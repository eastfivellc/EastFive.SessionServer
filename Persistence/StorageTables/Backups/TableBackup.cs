using EastFive.Azure.StorageTables.Driver;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Azure.Persistence.StorageTables;
using EastFive.Persistence;
using Newtonsoft.Json;
using EastFive.Api;
using EastFive.Persistence.Azure.StorageTables;
using System.Net.Http;
using EastFive.Azure.Functions;

namespace EastFive.Azure.Persistence.AzureStorageTables.Backups
{
    [FunctionViewController6(
        Route = "TableBackup",
        Resource = typeof(TableBackup),
        ContentType = "x-application/document-signing-flow",
        ContentTypeVersion = "0.1")]
    [StorageTable]
    public struct TableBackup : IReferenceable
    {
        #region Properties

        [JsonIgnore]
        public Guid id => tableBackupRef.id;

        public const string IdPropertyName = "id";
        [ApiProperty(PropertyName = IdPropertyName)]
        [JsonProperty(PropertyName = IdPropertyName)]
        [RowKey]
        public IRef<TableBackup> tableBackupRef;

        public const string WhenPropertyName = "when";
        [ApiProperty(PropertyName = WhenPropertyName)]
        [JsonProperty(PropertyName = WhenPropertyName)]
        [PartitionByDay()]
        public DateTime when;

        [Storage]
        [JsonIgnore]
        public string continuationToken;

        public const string TableNamePropertyName = "table_name";
        [ApiProperty(PropertyName = TableNamePropertyName)]
        [JsonProperty(PropertyName = TableNamePropertyName)]
        [Storage]
        public string tableName;

        public const string StorageSettingCopyFromPropertyName = "storage_setting_copy_from";
        [ApiProperty(PropertyName = StorageSettingCopyFromPropertyName)]
        [JsonProperty(PropertyName = StorageSettingCopyFromPropertyName)]
        [Storage]
        public string storageSettingCopyFrom;

        public const string StorageSettingCopyToPropertyName = "storage_setting_copy_to";
        [ApiProperty(PropertyName = StorageSettingCopyToPropertyName)]
        [JsonProperty(PropertyName = StorageSettingCopyToPropertyName)]
        [Storage]
        public string storageSettingCopyTo;

        #endregion

        #region Http Methods

        [HttpPost]
        public static async Task<HttpResponseMessage> CreateAsync(
                [Property(Name = IdPropertyName)]IRef<TableBackup> documentSourceRef,
                [Property(Name = WhenPropertyName)]DateTime when,
                [Property(Name = TableNamePropertyName)]string tableName,
                [Property(Name = StorageSettingCopyFromPropertyName)]string storageSettingCopyFrom,
                [Property(Name = StorageSettingCopyToPropertyName)]string storageSettingCopyTo,
                [Resource]TableBackup tableBackup,
                RequestMessage<TableBackup> requestQuery,
            CreatedBodyResponse<InvocationMessage> onCreated,
            AlreadyExistsResponse onAlreadyExists)
        {
            return await await tableBackup.StorageCreateAsync(
                async (entity) =>
                {
                    var invocationMessage = await requestQuery
                        .ById(documentSourceRef)
                        .HttpPatch(default)
                        .CompileRequest()
                        .FunctionAsync();

                    return onCreated(invocationMessage);
                },
                () => onAlreadyExists().AsTask());
        }

        [HttpPatch]
        public static async Task<HttpResponseMessage> UpdateAsync(
                [UpdateId(Name = IdPropertyName)]IRef<TableBackup> documentSourceRef,
                RequestMessage<TableBackup> requestQuery,
            CreatedBodyResponse<InvocationMessage> onContinued,
            NoContentResponse onComplete,
            NotFoundResponse onNotFound)
        {
            return await await documentSourceRef.StorageGetAsync(
                async entity =>
                {
                    var complete = await entity.Copy(TimeSpan.FromMinutes(8));
                    if (complete)
                        return onComplete();

                    var invocationMessage = await requestQuery
                        .ById(documentSourceRef)
                        .HttpPatch(default)
                        .CompileRequest()
                        .FunctionAsync();

                    return onContinued(invocationMessage);
                },
                () => onNotFound().AsTask());
        }

        #endregion

        #region Copy Functions

        public async Task<bool> Copy(
            TimeSpan limit)
        {
            var cloudStorageFromAccount = CloudStorageAccount.Parse(storageSettingCopyFrom);
            var cloudStorageToAccount = CloudStorageAccount.Parse(storageSettingCopyTo);
            var cloudStorageFromClient = cloudStorageFromAccount.CreateCloudTableClient();
            var cloudStorageToClient = cloudStorageToAccount.CreateCloudTableClient();

            var tableFrom = cloudStorageFromClient.GetTableReference(tableName);
            var tableTo = cloudStorageToClient.GetTableReference(tableName);
            var query = new TableQuery<GenericTableEntity>();

            var token = default(TableContinuationToken);
            if (continuationToken.HasBlackSpace())
            {
                token = new TableContinuationToken();
                var tokenReader = XmlReader.Create(new StringReader(continuationToken));
                token.ReadXml(tokenReader);
            }

            var timer = Stopwatch.StartNew();

            var segmentFecthing = tableFrom.ExecuteQuerySegmentedAsync(query, token);
            var resultsProcessing = new TableResult[] { }.AsTask();
            var backoff = TimeSpan.FromSeconds(1.0);
            while (true)
            {
                try
                {
                    var segment = await segmentFecthing;
                    if (segment.IsDefaultOrNull())
                    {
                        await resultsProcessing;
                        return true;
                    }

                    token = segment.ContinuationToken;
                    if (timer.Elapsed > limit)
                    {
                        var tokenTextBuilder = new StringBuilder();
                        token.WriteXml(XmlWriter.Create(tokenTextBuilder, new XmlWriterSettings()));
                        var tokenToSave = tokenTextBuilder.ToString();
                        bool saved = await this.tableBackupRef.StorageUpdateAsync(
                            async (backup, saveAsync) =>
                            {
                                backup.continuationToken = tokenToSave;
                                await saveAsync(backup);
                                return true;
                            },
                            () => false);
                        await resultsProcessing;
                        return false;
                    }

                    var priorResults = segment.Results.ToArray();
                    segmentFecthing = token.IsDefaultOrNull() ?
                        default
                        :
                        tableFrom.ExecuteQuerySegmentedAsync(query, token);
                    
                    var resultsProcessingNext = CreateOrReplaceBatch(priorResults, tableTo);
                    await resultsProcessing;
                    resultsProcessing = resultsProcessingNext;
                    backoff = TimeSpan.FromSeconds(1.0);
                    continue;
                }
                catch (StorageException storageEx)
                {
                    if (storageEx.IsProblemTimeout())
                    {
                        backoff = backoff + TimeSpan.FromSeconds(1.0);
                        await Task.Delay(backoff);
                        segmentFecthing = token.IsDefaultOrNull() ?
                            default
                            :
                            tableFrom.ExecuteQuerySegmentedAsync(query, token);
                        continue;
                    }
                }
                catch (AggregateException)
                {
                    throw;
                }
                catch (Exception)
                {
                    throw;
                };
            }
        }

        private static async Task<TableResult[]> CreateOrReplaceBatch(GenericTableEntity[] entities,
                CloudTable table)
        {
            return await entities
                .GroupBy(row => row.PartitionKey)
                .SelectMany(
                    grp =>
                    {
                        return grp
                            .Split(index => 100)
                            .Select(set => set.ToArray());
                    })
                .Select(grp => CreateOrReplaceBatchAsync(grp, table: table))
                .AsyncEnumerable()
                .SelectMany()
                .ToArrayAsync();
        }

        private static async Task<TableResult[]> CreateOrReplaceBatchAsync(GenericTableEntity[] entities,
            CloudTable table)
        {
            if (!entities.Any())
                return new TableResult[] { };

            var batch = new TableBatchOperation();
            var rowKeyHash = new HashSet<string>();
            foreach (var row in entities)
            {
                if (rowKeyHash.Contains(row.RowKey))
                {
                    continue;
                }
                batch.InsertOrReplace(row);
            }

            // submit
            while (true)
            {
                try
                {
                    var resultList = await table.ExecuteBatchAsync(batch);
                    return resultList.ToArray();
                }
                catch (StorageException storageException)
                {
                    var shouldRetry = await storageException.ResolveCreate(table,
                        () => true);
                    if (shouldRetry)
                        continue;

                }
            }
        }

        #endregion
    }
}

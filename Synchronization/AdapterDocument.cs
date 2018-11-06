using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.WindowsAzure.Storage.Table;

using BlackBarLabs;
using EastFive.Collections.Generic;

using BlackBarLabs.Persistence;
using BlackBarLabs.Extensions;
using EastFive.Serialization;
using BlackBarLabs.Persistence.Azure;
using EastFive.Extensions;
using EastFive;
using EastFive.Linq;
using BlackBarLabs.Persistence.Azure.StorageTables;
using System.Runtime.Serialization;
using EastFive.Linq.Async;

namespace EastFive.Azure.Synchronization.Persistence
{
    public class AdapterDocument : TableEntity
    {
        [IgnoreDataMember]
        [IgnoreProperty]
        public Guid Id => Guid.Parse(this.RowKey);
        
        public Guid IntegrationId { get; set; }

        public string ResourceType { get; set; }

        public string Key { get; set; }

        public string Name { get; set; }

        public byte[] IdentifiersKeys { get; set; }
        
        public byte[] IdentifiersValues { get; set; }
        
        public KeyValuePair<string, string>[] GetIdentifiers()
        {
            return this.IdentifiersKeys
                .ToStringsFromUTF8ByteArray()
                .Zip(this.IdentifiersValues.ToStringsFromUTF8ByteArray(),
                    (k, v) => k.PairWithValue(v))
                .ToArray();
        }

        private void SetIdentifiers(KeyValuePair<string, string>[] identifiers)
        {
            this.IdentifiersKeys = identifiers.NullToEmpty().SelectKeys().ToUTF8ByteArrayOfStrings();
            this.IdentifiersValues = identifiers.NullToEmpty().SelectValues().ToUTF8ByteArrayOfStrings();
        }

        #region ConnectorIds

        public byte[] ConnectorIds { get; set; }

        internal Guid[] GetConnectorIds()
        {
            return ConnectorIds.ToGuidsFromByteArray();
        }

        internal bool SetConnectorIds(Guid [] connectorIds)
        {
            this.ConnectorIds = connectorIds.ToByteArrayOfGuids();
            return true;
        }

        internal bool AddConnectorId(Guid orderItemId)
        {
            var orderItems = this.GetConnectorIds();
            if (orderItems.Contains(orderItemId))
                return false;
            this.ConnectorIds = orderItems.Append(orderItemId).ToByteArrayOfGuids();
            return true;
        }

        internal bool RemoveConnectorId(Guid orderItemId)
        {
            var orderItems = this.ConnectorIds.ToGuidsFromByteArray();
            if (!orderItems.Contains(orderItemId))
                return false;
            this.ConnectorIds = orderItems.Where(oi => oi != orderItemId).ToByteArrayOfGuids();
            return true;
        }

        #endregion

        public Guid GetId()
        {
            return AdapterDocument.GetId(this.Key, this.IntegrationId, this.ResourceType);
        }

        public static Guid GetId(string key, Guid integrationId, string resourceType)
        {
            return $"{key}/{integrationId.ToString("N")}/{resourceType}".MD5HashGuid();
        }

        public static Task<TResult> FindByIdAsync<TResult>(Guid adapterId,
            Func<Adapter, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository => azureStorageRepository.FindByIdAsync(adapterId,
                (AdapterDocument adapterDoc) =>
                {
                    return onFound(Convert(adapterDoc));
                },
                onNotFound));
        }

        public static Task<TResult> FindByKeyAsync<TResult>(string key, Guid integrationId, string resourceType,
            Func<Adapter, TResult> onFound,
            Func<TResult> onNotFound)
        {
            var adapterId = GetId(key, integrationId, resourceType);
            return AzureStorageRepository.Connection(
                azureStorageRepository => azureStorageRepository.FindByIdAsync(adapterId,
                (AdapterDocument adapterDoc) =>
                {
                    return onFound(Convert(adapterDoc));
                },
                onNotFound));
        }

        public static IEnumerableAsync<Adapter> FindAll(Guid integrationId, string resourceType)
        {
            if (resourceType.IsNullOrWhiteSpace())
                return EnumerableAsync.Empty<Adapter>();

            return AzureStorageRepository.Connection(
                azureStorageRepository =>
                {
                    var whereIntegrationQuery = TableQuery.GenerateFilterConditionForGuid("IntegrationId", QueryComparisons.Equal, integrationId);
                    var whereResourceTypeQuery = TableQuery.GenerateFilterCondition("ResourceType", QueryComparisons.Equal, resourceType);
                    var whereQuery = TableQuery.CombineFilters(whereIntegrationQuery, TableOperators.And, whereResourceTypeQuery);
                    var adapterQuery = new TableQuery<AdapterDocument>().Where(whereQuery);
                    return azureStorageRepository
                        .FindAllAsync(adapterQuery)
                        .Select(Convert);
                });
        }

        public static IEnumerableAsync<Adapter> FindAll(string resourceType)
        {
            if (resourceType.IsNullOrWhiteSpace())
                return EnumerableAsync.Empty<Adapter>();

            return AzureStorageRepository.Connection(
                azureStorageRepository =>
                {
                    var whereResourceTypeQuery = TableQuery.GenerateFilterCondition("ResourceType", QueryComparisons.Equal, resourceType);
                    var adapterQuery = new TableQuery<AdapterDocument>().Where(whereResourceTypeQuery);
                    return azureStorageRepository
                        .FindAllAsync(adapterQuery)
                        .Select(Convert);
                });
        }


        internal static Adapter Convert(AdapterDocument syncDoc)
        {
            return new Adapter
            {
                adapterId = syncDoc.Id,
                key = syncDoc.Key,
                integrationId = syncDoc.IntegrationId,
                name = syncDoc.Name,
                identifiers = syncDoc.GetIdentifiers(),
                connectorIds = syncDoc.GetConnectorIds(),
                resourceType = syncDoc.ResourceType,
            };
        }

        public static Task<TResult> FindOrCreateAsync<TResult>(string key, Guid integrationId, string resourceType,
            Func<bool, Adapter, Func<Adapter, Task<Guid>>, Task<TResult>> onFound)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository =>
                {
                    var adapterId = GetId(key, integrationId, resourceType);
                    return azureStorageRepository.CreateOrUpdateAsync<AdapterDocument, TResult>(adapterId,
                        (created, adapterDoc, saveAsync) =>
                        {
                            adapterDoc.Key = key;
                            adapterDoc.IntegrationId = integrationId;
                            adapterDoc.ResourceType = resourceType;
                            return onFound(created, Convert(adapterDoc),
                                async (adapterUpdated) =>
                                {
                                    adapterDoc.IntegrationId = adapterUpdated.integrationId;
                                    adapterDoc.Key = adapterUpdated.key;
                                    adapterDoc.Name = adapterUpdated.name;
                                    adapterDoc.ResourceType = resourceType; // Shim
                                    adapterDoc.SetIdentifiers(adapterUpdated.identifiers);
                                    adapterDoc.SetConnectorIds(adapterUpdated.connectorIds);
                                    await saveAsync(adapterDoc);
                                    return adapterId;
                                });
                        });
                });
        }
        
        internal static IEnumerableAsync<Adapter> CreateOrUpdateBatch(IEnumerableAsync<string> keys, Guid integrationId, string resourceType)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository =>
                {
                    var adapters = keys
                        .Select(
                            key =>
                            { 
                                var adapter = new AdapterDocument()
                                {
                                    Key = key,
                                    IntegrationId = integrationId,
                                    ResourceType = resourceType,
                                };
                                return adapter;
                            });
                    return azureStorageRepository
                        .CreateOrReplaceBatch(adapters,
                                adapter => GetId(adapter.Key, integrationId, resourceType),
                            (successAdapter) => successAdapter,
                            (failedAdapter) => failedAdapter)
                        .Select(adapter => Convert(adapter));
                });
        }

        internal static IEnumerableAsync<Adapter> CreateOrUpdateBatch(IEnumerable<string> keys, Guid integrationId, string resourceType)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository =>
                {
                    var adapters = keys
                        .Select(
                            key =>
                            {
                                var adapter = new AdapterDocument()
                                {
                                    Key = key,
                                    IntegrationId = integrationId,
                                    ResourceType = resourceType,
                                };
                                return adapter;
                            });
                    return azureStorageRepository
                        .CreateOrReplaceBatch(adapters,
                                adapter => GetId(adapter.Key, integrationId, resourceType),
                            (successAdapter) => successAdapter,
                            (failedAdapter) => failedAdapter)
                        .Select(adapter => Convert(adapter));
                });
        }
        
        internal static Task<TResult> UpdateAsync<TResult>(Guid sourceAdapterId,
            Func<Adapter, Func<Guid[], string, KeyValuePair<string, string>[], Task>, Task<TResult>> onFound,
            Func<TResult> onNotFound)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository =>
                {
                    return azureStorageRepository.UpdateAsync<AdapterDocument, TResult>(sourceAdapterId,
                        (adapterDoc, saveAsync) =>
                        {
                            var adapter = Convert(adapterDoc);
                            return onFound(adapter,
                                (connectorIds, name, identifiers) =>
                                {
                                    adapterDoc.SetConnectorIds(connectorIds);
                                    adapterDoc.Name = name;
                                    adapterDoc.SetIdentifiers(identifiers);
                                    return saveAsync(adapterDoc);
                                });
                        });
                });
        }
        
        internal static Task<TResult> DeleteByIdAsync<TResult>(Guid synchronizationId,
            Func<TResult> onDeleted,
            Func<TResult> onNotFound)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository =>
                {
                    return azureStorageRepository.DeleteIfAsync<ConnectorDocument, TResult>(synchronizationId,
                        (syncDoc, deleteSyncDocAsync) =>
                        {
                            throw new NotImplementedException();
                            // return onDeleted();
                            //if (syncDoc.LocalIdMaybe.HasValue)
                            //{
                            //    bool successLocal = await azureStorageRepository.DeleteIfAsync<ConnectorDocument, bool>(syncDoc.LocalIdMaybe.Value,
                            //        async (syncLookupDoc, deleteSyncLookupDocAsync) =>
                            //        {
                            //            await deleteSyncLookupDocAsync();
                            //            return true;
                            //        },
                            //        () => false);
                            //}
                            //if (!syncDoc.RemoteId.IsNullOrWhiteSpace())
                            //{
                            //    var externalLookupId = ConnectorDocument.GetId(syncDoc.SystemName, syncDoc.ResourceType, syncDoc.ActorId, syncDoc.RemoteId);
                            //    bool successExternal = await azureStorageRepository.DeleteIfAsync<ConnectorDocument, bool>(externalLookupId,
                            //        async (syncLookupDoc, deleteSyncLookupDocAsync) =>
                            //        {
                            //            await deleteSyncLookupDocAsync();
                            //            return true;
                            //        },
                            //        () => false);
                            //}
                            //bool deletedLookup = await azureStorageRepository.UpdateAsync<SynchronizationActorLookupDocument, bool>(syncDoc.ActorId, syncDoc.SystemName,
                            //    async (syncActorLookupDoc, saveAsync) =>
                            //    {
                            //        if (syncActorLookupDoc.RemoveSynchronizationDocumentId(synchronizationId))
                            //            await saveAsync(syncActorLookupDoc);
                            //        return true;
                            //    },
                            //    () => false);

                            //await deleteSyncDocAsync();
                            //return onDeleted();
                        },
                        onNotFound);
                });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <typeparam name="TResultInner"></typeparam>
        /// <param name="actorId"></param>
        /// <param name="systemName"></param>
        /// <param name="shouldDelete">LocalId, RemoteId, ResourceType</param>
        /// <param name="onSuccess"></param>
        /// <returns></returns>
        public Task<TResult> DeleteByActorAndSystemAsync<TResult, TResultInner>(Guid actorId, Guid integrationId,
            Func<Guid, string, string, Func<Task>, Task<TResultInner>> shouldDelete,
            Func<IEnumerable<TResultInner>, TResult> onSuccess)
        {

            return AzureStorageRepository.Connection(
                async azureStorageRepository =>
                {
                    throw new NotImplementedException();
                    return onSuccess(null);
                    //    var integrationsResults = await await azureStorageRepository
                    //.FindLinkedDocumentsAsync<SynchronizationActorLookupDocument, SynchronizationDocument, Task<TResult>>(
                    //        actorId, systemName,
                    //        (actorLookupDoc) => actorLookupDoc.GetSynchronizationDocumentIds(),
                    //    async (actorLookupDoc, syncDocs) =>
                    //    {
                    //        var resultsInner = await syncDocs
                    //            .Select(
                    //                async syncDoc =>
                    //                {
                    //                    bool keepIt = true;
                    //                    var localId = syncDoc.LocalIdMaybe.HasValue ? syncDoc.LocalIdMaybe.Value : syncDoc.LocalId;
                    //                    var result = await shouldDelete(localId, syncDoc.RemoteId, syncDoc.ResourceType,
                    //                        async () =>
                    //                        {
                    //                            keepIt = await azureStorageRepository.DeleteAsync(syncDoc,
                    //                                () => false,
                    //                                () => false);
                    //                            var deletedRef = await azureStorageRepository.UpdateAsync<SynchronizationLookupDocument, bool>(localId,
                    //                                async (doc, updateAsync) =>
                    //                                {
                    //                                    if (doc.RemoveSynchronizationDocumentId(syncDoc.Id))
                    //                                        await updateAsync(doc);
                    //                                    return true;
                    //                                },
                    //                                () => false);
                    //                            deletedRef.GetType();
                    //                        });
                    //                    var response = syncDoc.PairWithValue(result);
                    //                    return response.PairWithValue(keepIt);
                    //                })
                    //            .WhenAllAsync(1);
                    //        actorLookupDoc.SynchronizationDocumentIds = resultsInner
                    //            .Where(ri => ri.Value)
                    //            .Select(ri => ri.Key.Key.Id)
                    //            .ToByteArrayOfGuids();
                    //        return await this.azureStorageRepository.UpdateIfNotModifiedAsync(actorLookupDoc,
                    //            () => onSuccess(resultsInner.SelectKeys().SelectValues().ToArray()),
                    //            () => onSuccess(resultsInner.SelectKeys().SelectValues().ToArray()));
                    //    },
                    //    () => onSuccess(new TResultInner[] { }).ToTask());

                    //    return integrationsResults;
                });
        }

        internal Task<TResult> UpdateAsync<TResult>(Guid synchronizationId,
            Func<Guid?, string, Func<Guid?, string, Task>, Task<TResult>> onFound,
            Func<TResult> onNotFound)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository =>
                {
                    return azureStorageRepository.UpdateAsync<ConnectorDocument, TResult>(synchronizationId,
                        async (doc, update) =>
                        {
                            return default(TResult);
                            //return onFound(doc.LocalIdMaybe, doc.RemoteId,
                            //    async (localId, remoteId) =>
                            //    {
                            //        doc.LocalIdMaybe = localId;
                            //        doc.RemoteId = remoteId;
                            //        await update(doc);
                            //    });
                        },
                        onNotFound);
                });
        }
    }
}

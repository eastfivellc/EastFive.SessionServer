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

namespace EastFive.Azure.Synchronization.Persistence
{
    public class ConnectorDocument : TableEntity
    {
        [IgnoreDataMember]
        [IgnoreProperty]
        public Guid Id => Guid.Parse(this.RowKey);

        public Guid LocalAdapter { get; set; }

        public Guid RemoteAdapter { get; set; }
        
        public Guid CreatedBy { get; set; }
        
        public string Method { get; set; }

        public Connector.SynchronizationMethod GetMethod()
        {
            Enum.TryParse(this.Method, out Connector.SynchronizationMethod result);
            return result;
        }

        internal void SetMethod(Connector.SynchronizationMethod method)
        {
            this.Method = Enum.GetName(typeof(Connector.SynchronizationMethod), method);
        }

        public Guid GetId()
        {
            return ConnectorDocument.GetId(this.LocalAdapter, this.RemoteAdapter);
        }

        public static Guid GetId(Guid localId, Guid remoteId)
        {
            return localId.ToByteArray().Concat(remoteId.ToByteArray()).ToArray().MD5HashGuid();
        }

        public static Task<TResult> CreateAsync<TResult>(Guid connectorId,
                Guid adapterInternalId, Guid adapterExternalId, Guid createdBy, Connector.SynchronizationMethod method,
            Func<TResult> onCreated,
            Func<TResult> onAlreadyExists,
            Func<Guid, TResult> onAdapterDoesNotExist)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository =>
                {
                    var lookupId = GetId(adapterInternalId, adapterExternalId);
                    var rollback = new RollbackAsync<TResult>();
                    rollback.AddTaskCreate(connectorId,
                        new ConnectorDocument()
                        {
                            CreatedBy = createdBy,
                            LocalAdapter = adapterInternalId,
                            RemoteAdapter = adapterExternalId,
                            Method = Enum.GetName(typeof(Connector.SynchronizationMethod), method),
                        },
                        onAlreadyExists,
                        azureStorageRepository);
                    rollback.AddTaskCreate(lookupId,
                        new EastFive.Persistence.Azure.Documents.LookupDocument()
                        {
                            Lookup = connectorId,
                        },
                        onAlreadyExists,
                        azureStorageRepository);
                    rollback.AddTaskUpdate<Guid[], TResult, AdapterDocument>(adapterInternalId,
                        (adapterDoc, updated, noChange, onReject) =>
                        {
                            var connectorIds = adapterDoc.GetConnectorIds();
                            if (adapterDoc.AddConnectorId(connectorId))
                                return updated(connectorIds);
                            return noChange();
                        },
                        (connectorIds, adapterDoc) => adapterDoc.SetConnectorIds(connectorIds),
                        "Rejection is not an option".AsFunctionException<TResult>(),
                        () => onAdapterDoesNotExist(adapterInternalId),
                        azureStorageRepository);
                    rollback.AddTaskUpdate<Guid[], TResult, AdapterDocument>(adapterExternalId,
                        (adapterDoc, updated, noChange, onReject) =>
                        {
                            var connectorIds = adapterDoc.GetConnectorIds();
                            if (adapterDoc.AddConnectorId(connectorId))
                                return updated(connectorIds);
                            return noChange();
                        },
                        (connectorIds, adapterDoc) => adapterDoc.SetConnectorIds(connectorIds),
                        "Rejection is not an option".AsFunctionException<TResult>(),
                        () => onAdapterDoesNotExist(adapterInternalId),
                        azureStorageRepository);
                    return rollback.ExecuteAsync(onCreated);
                });
        }

        internal static Task<TResult> FindByIdAsync<TResult>(Guid connectorId, 
            Func<Connector, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository => azureStorageRepository.FindByIdAsync(connectorId,
                (ConnectorDocument adapterDoc) =>
                {
                    return onFound(Convert(adapterDoc));
                },
                onNotFound));
        }

        internal static Task<TResult> FindByAdapterAsync<TResult>(Guid adapterId,
            Func<Adapter, Connector[], TResult> onFound,
            Func<TResult> onAdapterNotFound)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository => azureStorageRepository.FindLinkedDocumentsAsync(adapterId,
                    (adapterDoc) => adapterDoc.GetConnectorIds(),
                    (AdapterDocument adapterDoc, ConnectorDocument[] connectorDocs) =>
                        onFound(AdapterDocument.Convert(adapterDoc), connectorDocs.Select(Convert).ToArray()),
                    onAdapterNotFound));
        }

        internal static Task<TResult> FindByAdapterWithConnectionAsync<TResult>(Guid adapterId,
            Func<Adapter, KeyValuePair<Connector, Adapter>[], TResult> onFound,
            Func<TResult> onAdapterNotFound)
        {
            return AzureStorageRepository.Connection(
                async azureStorageRepository => await await azureStorageRepository.FindLinkedDocumentsAsync(adapterId,
                    (adapterDoc) => adapterDoc.GetConnectorIds(),
                    async (AdapterDocument adapterDoc, ConnectorDocument[] connectorDocs) =>
                        await connectorDocs
                            .FlatMap(
                                async (connectorDoc, next, skip) =>
                                    await await AdapterDocument.FindByIdAsync(connectorDoc.RemoteAdapter,
                                        adapter => next(Convert(connectorDoc).PairWithValue(adapter)),
                                        () => skip()),
                                    (IEnumerable<KeyValuePair<Connector, Adapter>> asdf) =>
                                        onFound(AdapterDocument.Convert(adapterDoc), asdf.ToArray()).ToTask()),
                    onAdapterNotFound.AsAsyncFunc()));
        }

        internal static Task<TResult> FindByIdWithAdapterRemoteAsync<TResult>(Guid connectorId,
            Func<Connector, Adapter, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository => azureStorageRepository.FindLinkedDocumentAsync(connectorId,
                    (ConnectorDocument connectorDoc) => connectorDoc.RemoteAdapter,
                    (ConnectorDocument connectorDoc, AdapterDocument AdapterDoc) =>
                        onFound(Convert(connectorDoc), AdapterDocument.Convert(AdapterDoc)),
                    onNotFound,
                    (connectorDoc) => onNotFound()));
        }

        public static Task<T> FindAllAsync<T>(Guid actorId, Guid integrationId, string resourceType,
            Func<Connector[], T> callback)
        {
            throw new NotImplementedException();
            return AzureStorageRepository.Connection(
                async azureStorageRepository =>
                {
                    var localToRemoteIds = await azureStorageRepository
                        .FindAllAsync(
                            (ConnectorDocument[] adaptersAll) =>
                                adaptersAll
                                    .Select(Convert)
                                    .ToArray());
                    return callback(localToRemoteIds);
                });
        }

        internal static Connector Convert(ConnectorDocument syncDoc)
        {
            return new Connector
            {
                connectorId = syncDoc.Id,
                synchronizationMethod = syncDoc.GetMethod(),
                createdBy = syncDoc.CreatedBy,
                adapterExternalId = syncDoc.RemoteAdapter,
                adapterInternalId = syncDoc.LocalAdapter,
            };
        }

        public delegate Task<Connector> UpdateDelegate(string externalId, Connector.SynchronizationMethod method,
            string createdBy, Adapter identifiersInternal, Adapter identifiersExternal);

        
        internal static Task<TResult> DeleteByIdAsync<TResult>(Guid connectorId,
            Func<Guid, Guid, TResult> onDeleted,
            Func<TResult> onNotFound)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository =>
                {
                    return azureStorageRepository.DeleteIfAsync<ConnectorDocument, TResult>(connectorId,
                        async (syncDoc, deleteConnectorDocAsync) =>
                        {
                            var lookupId = GetId(syncDoc.LocalAdapter, syncDoc.RemoteAdapter);
                            var deleteLookupTask = azureStorageRepository.DeleteIfAsync<EastFive.Persistence.Azure.Documents.LookupDocument, bool>(lookupId,
                                async (lookupDoc, deleteLookupDocAsync) =>
                                {
                                    await deleteLookupDocAsync();
                                    return true;
                                },
                                () => false);
                            await deleteConnectorDocAsync();
                            bool deleted = await deleteLookupTask;
                            return onDeleted(syncDoc.LocalAdapter, syncDoc.RemoteAdapter);
                        },
                        onNotFound);
                });
        }
    }
}

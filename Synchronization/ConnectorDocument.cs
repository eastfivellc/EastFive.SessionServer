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
using BlackBarLabs.Linq.Async;
using EastFive.Linq.Async;

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
                Guid adapterInternalId, Guid adapterExternalId, Connector.SynchronizationMethod method,
            Func<TResult> onCreated,
            Func<TResult> onAlreadyExists,
            Func<Func<Task<Guid>>, TResult> onRelationshipAlreadyExists,
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
                        () => onRelationshipAlreadyExists(
                            () => azureStorageRepository.FindByIdAsync(lookupId,
                                (EastFive.Persistence.Azure.Documents.LookupDocument lookupDoc) => lookupDoc.Lookup,
                                () => default(Guid))),
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


        public static Task<TResult> UpdateAsync<TResult>(Guid connectorId,
            Func<Connector, Func<Connector.SynchronizationMethod, Task>, Task<TResult>> onUpdated,
            Func<TResult> onConnectorDoesNotExist)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository =>
                {
                    return azureStorageRepository.UpdateAsync<ConnectorDocument, TResult>(connectorId,
                        (connectorDoc, saveAsync) =>
                        {
                            return onUpdated(Convert(connectorDoc),
                                async (method) =>
                                {
                                    connectorDoc.Method = Enum.GetName(typeof(Connector.SynchronizationMethod), method);
                                    await saveAsync(connectorDoc);
                                });
                        },
                        () => onConnectorDoesNotExist());
                });
        }

        internal static Task<TResult> FindByIdAsync<TResult>(Guid connectorId, 
            Func<Connector, Guid, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository =>
                    azureStorageRepository.FindLinkedDocumentAsync(connectorId,
                        (ConnectorDocument connectorDoc) => connectorDoc.RemoteAdapter,
                        (ConnectorDocument adapterDoc, AdapterDocument remoteAdapterDoc) =>
                        {
                            return onFound(Convert(adapterDoc), remoteAdapterDoc.IntegrationId);
                        },
                        onNotFound,
                        (parentDoc) => onNotFound()));
        }

        internal static Task<TResult> FindByAdapterAsync<TResult>(Guid adapterId,
            Func<Adapter, KeyValuePair<Connector, Guid>[], TResult> onFound,
            Func<TResult> onAdapterNotFound)
        {
            return AzureStorageRepository.Connection(
                async repo => await await repo.FindByIdAsync(adapterId,
                    async (AdapterDocument adapterDoc) =>
                    {
                        var connectorIds = adapterDoc.GetConnectorIds();
                        var connections = await connectorIds
                            .Select(
                                connectorId => FindByIdAsync(connectorId,
                                    (connector, externalIntegrationId) => connector.PairWithValue(externalIntegrationId),
                                    () => default(KeyValuePair<Connector, Guid>?)))
                            .WhenAllAsync()
                            .SelectWhereHasValueAsync()
                            .ToArrayAsync();
                        if(connections.Length < connectorIds.Length)
                        {
                            var updatedConnectorIds = connections
                                .Select(connection => connection.Key.connectorId)
                                .ToArray();
                            adapterDoc.SetConnectorIds(updatedConnectorIds);
                            bool didUpdatedAdapter = await repo.UpdateIfNotModifiedAsync(adapterDoc,
                                () => true,
                                () => false);
                        }
                        var adapter = AdapterDocument.Convert(adapterDoc);
                        return onFound(adapter, connections);
                    },
                    onAdapterNotFound.AsAsyncFunc()));
        }

        internal static Task<TResult> FindByAdapterAsync<TResult>(Adapter adapter,
            Func<KeyValuePair<Connector, Guid>[], TResult> onFound,
            Func<TResult> onAdapterNotFound)
        {
            return AzureStorageRepository.Connection(
                async repo =>
                    {
                        var connectorIds = adapter.connectorIds;
                        var connections = await connectorIds
                            .Select(
                                connectorId => FindByIdAsync(connectorId,
                                    (connector, externalIntegrationId) => connector.PairWithValue(externalIntegrationId),
                                    () => default(KeyValuePair<Connector, Guid>?)))
                            .WhenAllAsync()
                            .SelectWhereHasValueAsync()
                            .ToArrayAsync();
                        
                        return onFound(connections);
                    });
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
                                    await await AdapterDocument.FindByIdAsync(
                                            connectorDoc.LocalAdapter == adapterId?
                                                connectorDoc.RemoteAdapter
                                                :
                                                connectorDoc.LocalAdapter,
                                        adapter => next(Convert(connectorDoc).PairWithValue(adapter)),
                                        () => skip()),
                                    (IEnumerable<KeyValuePair<Connector, Adapter>> connectedAdapters) =>
                                        onFound(AdapterDocument.Convert(adapterDoc), connectedAdapters.ToArray()).ToTask()),
                    onAdapterNotFound.AsAsyncFunc()));
        }

        internal static Task<TResult> FindByAdapterWithConnectionAsync<TResult>(Adapter adapter,
            Func<KeyValuePair<Connector, Adapter>[], TResult> onFound,
            Func<TResult> onAdapterNotFound)
        {
            return AzureStorageRepository.Connection(
                async azureStorageRepository =>
                {
                    var connections = await adapter.connectorIds
                        .Select(
                            connectorId => azureStorageRepository.FindLinkedDocumentAsync(connectorId,
                                (ConnectorDocument connectorDoc) =>
                                    connectorDoc.LocalAdapter == adapter.adapterId ?
                                        connectorDoc.RemoteAdapter
                                        :
                                        connectorDoc.LocalAdapter,
                                (ConnectorDocument connectorDoc, AdapterDocument adapterDoc) =>
                                    Convert(connectorDoc).PairWithValue(AdapterDocument.Convert(adapterDoc)),
                                () => default(KeyValuePair<Connector, Adapter>?),
                                (connectorDoc) => default(KeyValuePair<Connector, Adapter>?)))
                        .WhenAllAsync()
                        .SelectWhereHasValueAsync()
                        .ToArrayAsync();
                    return onFound(connections);
                });
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

        public static IEnumerableAsync<Connection> FindAllByType(string resourceType)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository =>
                {
                    var query = new TableQuery<ConnectorDocument>();
                    var connectors = azureStorageRepository.FindAllAsync(query);
                    return connectors
                        .SelectAsyncOptional<ConnectorDocument, Connection>(
                            async (connector, select, skip) => await await AdapterDocument.FindByIdAsync(connector.LocalAdapter,
                                async adapterInternal =>
                                {
                                    if (adapterInternal.resourceType != resourceType)
                                        return skip();
                                    return await AdapterDocument.FindByIdAsync(connector.RemoteAdapter,
                                        adapterExternal => select(
                                            new Connection()
                                            {
                                                connector = Convert(connector),
                                                adapterInternal = adapterInternal,
                                                adapterExternal = adapterExternal,
                                            }),
                                        () => skip());
                                },
                                () => skip().ToTask()));
                });
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
                connection =>
                {
                    return connection.DeleteIfAsync<ConnectorDocument, TResult>(connectorId,
                        async (syncDoc, deleteConnectorDocAsync) =>
                        {
                            var updatedLocalTask = connection.UpdateAsync<AdapterDocument, bool>(syncDoc.LocalAdapter,
                                async (adapterDoc, saveAdapterAsync) =>
                                {
                                    if (adapterDoc.RemoveConnectorId(connectorId))
                                        await saveAdapterAsync(adapterDoc);
                                    return true;
                                },
                                () => false);
                            var updatedRemoteTask = connection.UpdateAsync<AdapterDocument, bool>(syncDoc.RemoteAdapter,
                                async (adapterDoc, saveAdapterAsync) =>
                                {
                                    if (adapterDoc.RemoveConnectorId(connectorId))
                                        await saveAdapterAsync(adapterDoc);
                                    return true;
                                },
                                () => false);
                            var lookupId = GetId(syncDoc.LocalAdapter, syncDoc.RemoteAdapter);
                            var deleteLookupTask = connection.DeleteIfAsync<EastFive.Persistence.Azure.Documents.LookupDocument, bool>(lookupId,
                                async (lookupDoc, deleteLookupDocAsync) =>
                                {
                                    await deleteLookupDocAsync();
                                    return true;
                                },
                                () => false);
                            await deleteConnectorDocAsync();
                            bool deleted = await deleteLookupTask;
                            bool updatedLocal = await updatedLocalTask;
                            bool updatedRemote = await updatedRemoteTask;
                            return onDeleted(syncDoc.LocalAdapter, syncDoc.RemoteAdapter);
                        },
                        onNotFound);
                });
        }

        internal static Task<TResult> CreateBatchAsync<TResult>(IEnumerable<KeyValuePair<Guid, Guid>> adapterIds,
                Connector.SynchronizationMethod method,
            Func<Guid[], Guid[], Guid[], TResult> onComplete)
        {
            return AzureStorageRepository.Connection(
                connection =>
                {
                    return connection.CreateOrReplaceBatchAsync(
                        adapterIds
                            .Select(
                                kvp =>
                                {
                                    var connectorDoc = new ConnectorDocument
                                    {
                                        LocalAdapter = kvp.Key,
                                        RemoteAdapter = kvp.Value,
                                    };
                                    connectorDoc.SetMethod(method);
                                    return connectorDoc;
                                })
                            .ToArray(),
                        (c) => Guid.NewGuid(),
                        (savedIds, failedIds) =>
                        {
                            return onComplete(savedIds, failedIds, new Guid[] { });
                        });
                });
        }
    }
}

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

    public class ConnectorSynchronizationDocument : TableEntity
    {
        [IgnoreDataMember]
        [IgnoreProperty]
        public Guid Id => Guid.Parse(this.RowKey);
        
        public long? WhenLast { get; set; }

        public bool Locked { get; set; }
    }

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

        private static Func<string, string> GetMutatePartitionKey(string resourceType) =>
            partition => $"{resourceType}___{partition}";

        public static Task<TResult> CreateAsync<TResult>(Guid connectorId,
                Guid adapterInternalId, Guid adapterExternalId, Connector.SynchronizationMethod method, string resourceType,
            Func<TResult> onCreated,
            Func<TResult> onAlreadyExists,
            Func<Func<Task<Connector>>, TResult> onRelationshipAlreadyExists,
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
                            async () => await await azureStorageRepository.FindByIdAsync(lookupId,
                                (EastFive.Persistence.Azure.Documents.LookupDocument lookupDoc) =>
                                    azureStorageRepository.FindByIdAsync(lookupDoc.Lookup,
                                        (ConnectorDocument connectorDoc) => Convert(connectorDoc),
                                        () => default(Connector)),
                                () => default(Connector).AsTask())),
                        azureStorageRepository);

                    rollback.AddTaskCreate(connectorId,
                            new ConnectorSynchronizationDocument
                            {
                                Locked = false,
                            },
                        () => onRelationshipAlreadyExists(
                            () => azureStorageRepository.FindByIdAsync(connectorId,
                                (ConnectorDocument connectorDoc) => Convert(connectorDoc),
                                () => default(Connector))),
                        azureStorageRepository,
                            mutatePartition:GetMutatePartitionKey(resourceType));

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
            Func<Connector, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository =>
                    azureStorageRepository.FindByIdAsync(connectorId,
                        (ConnectorDocument connectorDoc) => onFound(Convert(connectorDoc)),
                        onNotFound));
        }

        internal static async Task<TResult> FindByIdAsync<TResult>(Guid connectorId, Guid sourceIntegration,
            Func<Connector, Guid, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return await await AzureStorageRepository.Connection(
                azureStorageRepository =>
                    azureStorageRepository.FindLinkedDocumentAsync(connectorId,
                        (ConnectorDocument connectorDoc) => connectorDoc.RemoteAdapter,
                        async (ConnectorDocument connectorDoc, AdapterDocument remoteAdapterDoc) =>
                        {
                            var connector = Convert(connectorDoc);
                            if (remoteAdapterDoc.IntegrationId != sourceIntegration)
                                return onFound(connector, remoteAdapterDoc.IntegrationId);
                            return await azureStorageRepository.FindByIdAsync(connectorDoc.LocalAdapter,
                                (AdapterDocument localAdapterDoc) => onFound(connector, localAdapterDoc.IntegrationId),
                                onNotFound);
                        },
                        onNotFound.AsAsyncFunc(),
                        (parentDoc) => onNotFound().AsTask()));
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
                                connectorId => FindByIdAsync(connectorId, adapterDoc.IntegrationId,
                                    (connector, integrationIdExternal) => connector.PairWithValue(integrationIdExternal),
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
                                connectorId => FindByIdAsync(connectorId, adapter.integrationId,
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

        internal static ConnectorDocument Convert(Connector connector)
        {
            var connectorDoc = new ConnectorDocument
            {
                LocalAdapter = connector.adapterInternalId,
                RemoteAdapter = connector.adapterExternalId,
            };
            connectorDoc.SetId(connector.connectorId);
            connectorDoc.SetMethod(connector.synchronizationMethod);
            return connectorDoc;
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
                                    bool deletedSynchronization = await connection.DeleteIfAsync<ConnectorSynchronizationDocument, bool>(connectorId,
                                        async (syncDocReal, deleteSyncDocAsync) =>
                                        {
                                            await deleteSyncDocAsync();
                                            return true;
                                        },
                                        () => false,
                                        mutatePartition: GetMutatePartitionKey(adapterDoc.ResourceType));

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

        internal static IEnumerableAsync<Guid> CreateBatch(IEnumerableAsync<Connector> adapterIds,
                Connector.SynchronizationMethod method)
        {
            return AzureStorageRepository.Connection(
                connection =>
                {
                    return connection
                        .CreateOrReplaceBatch(
                                adapterIds
                                    .Select(
                                        connector =>
                                        {
                                            var connectorDoc = Convert(connector);
                                            return connectorDoc;
                                        }),
                                (c) => c.Id,
                            c => true.PairWithValue(c),
                            c => false.PairWithValue(c))
                        .Where(kvp => kvp.Key)
                        .Select(kvp => kvp.Value.Id);
                });
        }

        internal static IEnumerableAsync<Guid> CreateBatch(IEnumerable<Connector> adapterIds,
                Connector.SynchronizationMethod method)
        {
            
            return AzureStorageRepository.Connection(
                connection =>
                {
                    return connection
                        .CreateOrReplaceBatch(
                                adapterIds
                                    .Select(
                                        connector =>
                                        {
                                            var connectorDoc = Convert(connector);
                                            return connectorDoc;
                                        }),
                                (c) => c.Id,
                            c => true.PairWithValue(c),
                            c => false.PairWithValue(c))
                        .Where(kvp => kvp.Key)
                        .Select(kvp => kvp.Value.Id);
                });
        }

        public interface ILockResult<T>
        {
            T Result { get; }
        }

        private struct LockResult<T> : ILockResult<T>
        {
            private T result;

            public LockResult(T result)
            {
                this.result = result;
            }

            public T Result => result;
        }

        internal static Task<TResult> SynchronizeLockedAsync<TResult>(Guid connectorId, string resourceType,
            Func<TimeSpan?,
                Func<TResult, Task<ILockResult<TResult>>>, 
                Func<TResult, Task<ILockResult<TResult>>>, 
                Task<ILockResult<TResult>>> onLockAquired,
            Func<int,
                TimeSpan, 
                TimeSpan?,
                Func<Task<TResult>>,
                Func<Task<TResult>>,
                Task<TResult>> onAlreadyLocked,
            Func<TResult> onNotFound)
        {
            return AzureStorageRepository.Connection(
                async connection =>
                {
                    TimeSpan? ComputeDuration(ConnectorSynchronizationDocument connSyncDoc)
                    {
                        var duration = connSyncDoc.WhenLast.HasValue ?
                                (DateTime.UtcNow - new DateTime(connSyncDoc.WhenLast.Value, DateTimeKind.Utc))
                                :
                                default(TimeSpan?);
                        return duration;
                    }

                    var lockResult = await await connection.LockedUpdateAsync<ConnectorSynchronizationDocument, Task<ILockResult<TResult>>>(connectorId,
                        connSyncDoc => connSyncDoc.Locked,
                        (connSyncDoc, unlockAndUpdate, unlock) =>
                        {
                            var duration = ComputeDuration(connSyncDoc);
                            return onLockAquired(duration,
                                async r =>
                                {
                                    await unlockAndUpdate(
                                        (doc, save) =>
                                        {
                                            doc.WhenLast = DateTime.UtcNow.Ticks;
                                            return save(doc);
                                        });
                                    return new LockResult<TResult>(r);
                                },
                                async (r) =>
                                {
                                    await unlock();
                                    return new LockResult<TResult>(r);
                                }).AsTask();
                        },
                        async () =>
                        {
                            return await await connection.FindByIdAsync(connectorId,
                                async (ConnectorDocument connector) =>
                                {
                                    return await await connection.CreateAsync(connectorId,
                                        new ConnectorSynchronizationDocument
                                        {
                                            Locked = false,
                                            WhenLast = default(long?),
                                        },
                                        async () =>
                                        {
                                            var retryResult = await SynchronizeLockedAsync(connectorId, resourceType, onLockAquired, onAlreadyLocked, onNotFound);
                                            return new LockResult<TResult>(retryResult);
                                        },
                                        async () =>
                                        {
                                            var retryResult = await SynchronizeLockedAsync(connectorId, resourceType, onLockAquired, onAlreadyLocked, onNotFound);
                                            return new LockResult<TResult>(retryResult);
                                        },
                                        mutatePartition: GetMutatePartitionKey(resourceType));
                                },
                                () => (new LockResult<TResult>(onNotFound())).AsTask());
                        },
                        onAlreadyLocked:
                            async (retryCount, retryDuration, connSyncDoc, continueAquiring, force) =>
                            {
                                var duration = ComputeDuration(connSyncDoc);
                                var lockCompleteResponse = await onAlreadyLocked(retryCount, retryDuration, duration,
                                    async () =>
                                    {
                                        var lockResponse = await await continueAquiring();
                                        return lockResponse.Result;
                                    },
                                    async () =>
                                    {
                                        var lockResponse = await await force();
                                        return lockResponse.Result;
                                    });
                                return (new LockResult<TResult>(lockCompleteResponse)).AsTask<ILockResult<TResult>>();
                            },
                        mutatePartition: GetMutatePartitionKey(resourceType));
                    return lockResult.Result;
                });
        }
    }
}

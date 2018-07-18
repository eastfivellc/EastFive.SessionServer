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
        
        public async static Task<T> FindAllAsync<T>(Guid integrationId, string resourceType,
            Func<Adapter[], T> callback)
        {
            if (resourceType.IsNullOrWhiteSpace())
                return callback(new Adapter[] { });

            return await AzureStorageRepository.Connection(
                async azureStorageRepository =>
                {
                    var localToRemoteIds = await azureStorageRepository
                        .FindAllAsync(
                            (AdapterDocument[] adaptersAll) =>
                                adaptersAll
                                    .Where(adapter =>
                                        adapter.IntegrationId == integrationId &&
                                        (!adapter.ResourceType.IsNullOrWhiteSpace()) &&
                                        adapter.ResourceType.ToLower() == resourceType.ToLower())
                                    .Select(Convert)
                                    .ToArray());
                    return callback(localToRemoteIds);
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
                            return onFound(created, Convert(adapterDoc),
                                async (adapterUpdated) =>
                                {
                                    adapterDoc.IntegrationId = adapterUpdated.integrationId;
                                    adapterDoc.Key = adapterUpdated.key;
                                    adapterDoc.Name = adapterUpdated.name;
                                    adapterDoc.ResourceType = resourceType; // Shim
                                    adapterDoc.SetIdentifiers(adapterUpdated.identifiers);
                                    await saveAsync(adapterDoc);
                                    return adapterId;
                                });
                        });
                });
        }

        public static Task<TResult> FindOrCreateAsync<TResult>(string key, Guid integrationId, string resourceType,
                Guid integrationExternalId,
            Func<Adapter, KeyValuePair<Connector, Adapter>?, Func<Adapter, KeyValuePair<Connector, Adapter>?, Task<Connection>>, Task<TResult>> onFound,
            Func<Func<Adapter, KeyValuePair<Connector, Adapter>?, Task<Connection>>, Task<TResult>> onNotFound)
        {
            throw new NotImplementedException();
            return AzureStorageRepository.Connection<Task<TResult>>(
                async azureStorageRepository =>
                {
                    var adapterId = GetId(key, integrationId, resourceType);
                    var result = await await azureStorageRepository.FindLinkedDocumentsAsync<AdapterDocument, ConnectorDocument, Task<TResult>>(adapterId,
                        (lookupDoc) => lookupDoc.GetConnectorIds(),
                        async (adapterDoc, connectorDocs) =>
                        {
                            var matches = connectorDocs
                                //.Where(connectorDoc => connectorDoc.IntegrationId == integrationExternalId)
                                .ToArray();
                            if (!matches.Any())
                                return await onFound(Convert(adapterDoc), default(KeyValuePair<Connector, Adapter>?),
                                    (adapterUpdated, connectorAdapterMaybe) => UpdateAsync(adapterId,
                                        adapterUpdated.key, adapterUpdated.name, adapterUpdated.identifiers,
                                        connectorAdapterMaybe, resourceType, integrationId, integrationExternalId));

                            var match = matches.First();
                            if (matches.Length > 1)
                            {
                                bool[] deleted = await matches
                                    .Skip(1)
                                    .Select(
                                        matchToDelete => ConnectorDocument.DeleteByIdAsync(matchToDelete.Id,
                                            (adapterLocalId, adapterRemoteId) => true,
                                            () => false))
                                    .WhenAllAsync(1);
                            }
                            return await await AdapterDocument.FindByIdAsync(match.RemoteAdapter,
                                adapterRemote => onFound(Convert(adapterDoc), ConnectorDocument.Convert(match).PairWithValue(adapterRemote),
                                    (adapterUpdated, connectorAdapterMaybe) => UpdateAsync(adapterId, adapterUpdated.key, adapterUpdated.name, adapterUpdated.identifiers,
                                        connectorAdapterMaybe, resourceType, integrationId, integrationExternalId)),
                                "Resource deleted during update".AsFunctionException<Task<TResult>>());
                        },
                        () => onNotFound(
                            (adapterUpdated, connectorAdapterMaybe) => CreateAsync(adapterId, adapterUpdated.key, adapterUpdated.name, adapterUpdated.identifiers,
                                connectorAdapterMaybe, resourceType, integrationId, integrationExternalId)));
                    return result;
                });
        }

        private static Task<Connection> CreateAsync(Guid adapterId,
            string key, string name, KeyValuePair<string, string>[] identifiers,
            KeyValuePair<Connector, Adapter>? connectorAdapterMaybe,
            string resourceType, Guid integrationId, Guid integrationIdRemote)
        {
            throw new NotImplementedException();
            //return AzureStorageRepository.Connection<Task<Connection>>(
            //    async azureStorageRepository =>
            //    {
            //        var rollback = new RollbackAsync<Connection>();
                    
            //        var adapterDoc = new AdapterDocument()
            //        {
            //            Key = key,
            //            IntegrationId = integrationId,
            //            Name = name,
            //            ResourceType = resourceType,
            //        };
            //        adapterDoc.SetIdentifiers(identifiers);

            //        rollback.AddTaskCreateOrUpdate(integrationId,
            //            (created, integrationAdapterLookupDoc) => integrationAdapterLookupDoc.AddLookupDocumentId(adapterId),
            //            (IntegrationAdapterLookupDocument integrationAdapterLookupDoc) => integrationAdapterLookupDoc.RemoveSynchronizationDocumentId(adapterId),
            //            azureStorageRepository);
                    
            //        if (!connectorAdapterMaybe.HasValue)
            //        {
            //            rollback.AddTaskCreate(adapterId, adapterDoc, () => default(Connection), azureStorageRepository);
            //            return await rollback.ExecuteAsync(
            //                () => new Connection
            //                {
            //                    adapterInternal = Convert(adapterDoc),
            //                });
            //        }

            //        var connector = connectorAdapterMaybe.Value.Key;
            //        var adapterRemote = connectorAdapterMaybe.Value.Value;
            //        adapterRemote.adapterId = GetId(adapterRemote.key, adapterRemote.integrationId, adapterRemote.resourceType);

            //        var connectorDoc = new ConnectorDocument()
            //        {
            //            CreatedBy = connector.createdBy,
            //            LocalAdapter = adapterId,
            //            RemoteAdapter = adapterRemote.adapterId,
            //        };
            //        connectorDoc.SetMethod(connector.synchronizationMethod);
            //        adapterDoc.AddConnectorId(connector.connectorId);
            //        rollback.AddTaskCreate(connector.connectorId, connectorDoc, () => default(Connection), azureStorageRepository);

            //        var adapterRemoteDoc = new AdapterDocument()
            //        {
            //            Key = adapterRemote.key,
            //            IntegrationId = integrationIdRemote,
            //            Name = adapterRemote.name,
            //            ResourceType = resourceType,
            //        };
            //        adapterRemoteDoc.AddConnectorId(connector.connectorId);
            //        adapterRemoteDoc.SetIdentifiers(adapterRemote.identifiers);
            //        rollback.AddTaskCreate(adapterRemote.adapterId, adapterRemoteDoc, () => default(Connection), azureStorageRepository);

            //        rollback.AddTaskCreateOrUpdate(adapterRemote.integrationId,
            //            (created, integrationAdapterLookupDoc) => integrationAdapterLookupDoc.AddLookupDocumentId(adapterRemote.adapterId),
            //            (IntegrationAdapterLookupDocument integrationAdapterLookupDoc) => integrationAdapterLookupDoc.RemoveSynchronizationDocumentId(adapterRemote.adapterId),
            //            azureStorageRepository);

            //        rollback.AddTaskCreate(adapterId, adapterDoc, () => default(Connection), azureStorageRepository);

            //        return await rollback.ExecuteAsync(
            //            () => new Connection
            //            {
            //                adapterInternal = Convert(adapterDoc),
            //                adapterExternal = Convert(adapterRemoteDoc),
            //                connector = ConnectorDocument.Convert(connectorDoc),
            //            });
            //    });
        }

        private static Task<Connection> UpdateAsync(Guid adapterId,
            string key, string name, KeyValuePair<string, string>[] identifiers,
            KeyValuePair<Connector, Adapter>? connectorAdapterMaybe,
            string resourceType, Guid integrationId, Guid integrationIdRemote)
        {
            throw new NotImplementedException();
            //return AzureStorageRepository.Connection(
            //    async azureStorageRepository =>
            //    {
            //        var rollback = new RollbackAsync<Connection>();
                    
            //        rollback.AddTaskCreateOrUpdate(integrationId,
            //            (created, integrationAdapterLookupDoc) => integrationAdapterLookupDoc.AddLookupDocumentId(adapterId),
            //            (IntegrationAdapterLookupDocument integrationAdapterLookupDoc) => integrationAdapterLookupDoc.RemoveSynchronizationDocumentId(adapterId),
            //            azureStorageRepository);
                    
            //        var connection = new Connection
            //        {
            //            adapterInternal = new Adapter()
            //            {
            //                adapterId = adapterId,
            //                key = key,
            //                name = name,
            //                identifiers = identifiers,
            //                integrationId = integrationId,
            //                resourceType = resourceType,
            //            },
            //        };

            //        if (!connectorAdapterMaybe.HasValue)
            //        {
            //            rollback.AddTaskUpdate<KeyValuePair<string, KeyValuePair<string, string>[]>, Connection, AdapterDocument>(adapterId,
            //                (adapterDoc, onModified, onNotModified) =>
            //                {
            //                    var rollbackSave = adapterDoc.Name.PairWithValue(adapterDoc.GetIdentifiers());
            //                    adapterDoc.Name = name;
            //                    adapterDoc.SetIdentifiers(identifiers);
            //                    adapterDoc.ResourceType = resourceType;
            //                    return onModified(rollbackSave);
            //                },
            //                (save, adapterDoc) =>
            //                {
            //                    adapterDoc.Name = save.Key;
            //                    adapterDoc.SetIdentifiers(save.Value);
            //                    adapterDoc.ResourceType = resourceType;
            //                    return true;
            //                },
            //                () => connection,
            //                azureStorageRepository);
            //            return await rollback.ExecuteAsync(() => connection);
            //        }

            //        var connector = connectorAdapterMaybe.Value.Key;
            //        var adapterRemote = connectorAdapterMaybe.Value.Value;
            //        adapterRemote.adapterId = GetId(adapterRemote.key, adapterRemote.integrationId, adapterRemote.resourceType);

            //        var connectorDoc = new ConnectorDocument()
            //        {
            //            CreatedBy = connector.createdBy,
            //            LocalAdapter = adapterId,
            //            RemoteAdapter = adapterRemote.adapterId,
            //        };
            //        connectorDoc.SetMethod(connector.synchronizationMethod);
            //        rollback.AddTaskCreate(connector.connectorId, connectorDoc, () => default(Connection), azureStorageRepository);

            //        var adapterRemoteDoc = new AdapterDocument()
            //        {
            //            Key = adapterRemote.key,
            //            IntegrationId = integrationIdRemote,
            //            Name = adapterRemote.name,
            //            ResourceType = resourceType,
            //        };
            //        adapterRemoteDoc.AddConnectorId(connector.connectorId);
            //        adapterRemoteDoc.SetIdentifiers(adapterRemote.identifiers);
            //        rollback.AddTaskCreate(adapterRemote.adapterId, adapterRemoteDoc, () => default(Connection), azureStorageRepository);
                    
            //        rollback.AddTaskCreateOrUpdate(adapterRemote.integrationId,
            //            (created, integrationAdapterLookupDoc) => integrationAdapterLookupDoc.AddLookupDocumentId(adapterRemote.adapterId),
            //            (IntegrationAdapterLookupDocument integrationAdapterLookupDoc) => integrationAdapterLookupDoc.RemoveSynchronizationDocumentId(adapterRemote.adapterId),
            //            azureStorageRepository);
                    
            //        rollback.AddTaskUpdate<KeyValuePair<KeyValuePair<string, Guid[]>, KeyValuePair<string, string>[]>, Connection, AdapterDocument>(adapterId,
            //                (adapterDoc, onModified, onNotModified) =>
            //                {
            //                    var rollbackSave = adapterDoc.Name.PairWithValue(adapterDoc.GetConnectorIds()).PairWithValue(adapterDoc.GetIdentifiers());
            //                    adapterDoc.AddConnectorId(connector.connectorId);
            //                    adapterDoc.Name = name;
            //                    adapterDoc.SetIdentifiers(identifiers);
            //                    adapterDoc.ResourceType = resourceType;
            //                    return onModified(rollbackSave);
            //                },
            //                (save, adapterDoc) =>
            //                {
            //                    adapterDoc.Name = save.Key.Key;
            //                    adapterDoc.SetConnectorIds(save.Key.Value);
            //                    adapterDoc.SetIdentifiers(save.Value);
            //                    adapterDoc.ResourceType = resourceType;
            //                    return true;
            //                },
            //                () => connection,
            //                azureStorageRepository);
            //        return await rollback.ExecuteAsync(() => connection);
            //    });
        }

        internal static Task<TResult> DeleteByIdAsync<TResult>(Guid synchronizationId,
            Func<TResult> onDeleted,
            Func<TResult> onNotFound)
        {
            return AzureStorageRepository.Connection(
                azureStorageRepository =>
                {
                    return azureStorageRepository.DeleteIfAsync<ConnectorDocument, TResult>(synchronizationId,
                        async (syncDoc, deleteSyncDocAsync) =>
                        {
                            throw new NotImplementedException();
                            return onDeleted();
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

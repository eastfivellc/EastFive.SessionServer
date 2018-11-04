using BlackBarLabs;
using BlackBarLabs.Extensions;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Synchronization
{
    public struct Adapter
    {
        public Guid adapterId;
        public string key;
        public string name;
        public KeyValuePair<string, string>[] identifiers;
        public Guid integrationId;
        public string resourceType;
        public Guid[] connectorIds;
    }

    public struct Connector
    {
        public Guid connectorId;
        
        public Guid createdBy;

        public Guid adapterInternalId;
        public Guid adapterExternalId;

        public enum SynchronizationMethod
        {
            useInternal,
            useExternal,
            latest,
            ignore,
        }
        public SynchronizationMethod synchronizationMethod;
    }
    
    public struct Connection
    {
        public Connector connector;
        public Adapter adapterInternal;
        public Adapter adapterExternal;
    }

    public delegate Task<TResult> ResourceAllSynchronizationsAsync<TResult>(
        Func<IEnumerable<Adapter>, TResult> onFound,
        Func<string, TResult> onConnectionIssue,
        Func<string, TResult> onFailure,
        Func<TResult> onNotSupported);

    public interface IIntegrate
    {
        TResult IsResourceTypeSupported<TResult>(string resourceType, string key,
            Func<BlackBarLabs.Api.Resources.WebId, TResult> onSupportedByController,
            Func<TResult> onNotSupported);
    }

    public abstract class Connections
    {
        /// <summary>
        /// "Internal" integration id for convenience methods
        /// </summary>
        private static Guid defaultInternalIntegrationId = default(Guid);

        abstract public string ResourceType { get; }

        private IIntegrate service;

        public Connections(IIntegrate service)
        {
            this.service = service;
        }

        public Task<Adapter[]> SaveAdaptersAsync(Guid integrationId, Adapter[] adapters)
        {
            var resourceType = this.ResourceType;
            return adapters
                .Select(
                    adapter => Persistence.AdapterDocument.FindOrCreateAsync(adapter.key,
                        integrationId, resourceType,
                        async (created, adapterInternalStorage, saveAsync) =>
                        {
                            adapterInternalStorage.key = adapter.key; // SHIM?
                            adapterInternalStorage.name = adapter.name;
                            adapterInternalStorage.identifiers = adapter.identifiers;
                            adapterInternalStorage.integrationId = integrationId;
                            adapterInternalStorage.resourceType = resourceType;

                            // Update identifiers internally if there is no externally mapped resource
                            var adapterId = await saveAsync(adapterInternalStorage);
                            return adapterInternalStorage;
                        }))
                .WhenAllAsync();
        }

        public static Task<TResult> CreateOrUpdateConnection<TResult>(string resourceKeyInternal, Guid internalSystemItegrationId,
            string resourceKeyExternalSystem, Guid externalSystemIntegrationId,
            string resourceType,
            Func<Connector, TResult> onSuccess)
        {
            return Persistence.AdapterDocument.FindOrCreateAsync(resourceKeyInternal, internalSystemItegrationId, resourceType,
                (createdAdapterInteral, adapterInternal, saveAdapterInternalAsync) =>
                    Persistence.AdapterDocument.FindOrCreateAsync(resourceKeyExternalSystem, externalSystemIntegrationId, resourceType,
                        async (createdAdapterExternal, adapterExternal, saveAdapterExternalAsync) =>
                        {
                            var mutualConnections = adapterInternal.connectorIds.Intersect(adapterExternal.connectorIds);
                            var alreadyConnected = mutualConnections.Any();
                            if (alreadyConnected)
                            {
                                var connectorIdMutual = mutualConnections.First();
                                var connector = new Connector
                                {
                                    connectorId = connectorIdMutual,
                                    adapterExternalId = adapterExternal.adapterId,
                                    adapterInternalId = adapterInternal.adapterId,

                                    // BOLD assumptions based on this being a convenience method
                                    createdBy = adapterInternal.adapterId,
                                    synchronizationMethod = Connector.SynchronizationMethod.ignore,
                                };
                                return onSuccess(connector);
                            }
                            
                            var connectorId = Guid.NewGuid();
                            return await await Persistence.ConnectorDocument.CreateWithoutAdapterUpdateAsync(connectorId,
                                    adapterInternal.adapterId, adapterExternal.adapterId, Connector.SynchronizationMethod.ignore, resourceType,
                                async () =>
                                {
                                    adapterInternal.connectorIds = adapterInternal.connectorIds.Append(connectorId).ToArray();
                                    await saveAdapterInternalAsync(adapterInternal);
                                    adapterExternal.connectorIds = adapterExternal.connectorIds.Append(connectorId).ToArray();
                                    await saveAdapterExternalAsync(adapterExternal);
                                    return onSuccess(
                                        new Connector
                                        {
                                            connectorId = connectorId,
                                            adapterExternalId = adapterExternal.adapterId,
                                            adapterInternalId = adapterInternal.adapterId,
                                            createdBy = adapterInternal.adapterId,
                                            synchronizationMethod = Connector.SynchronizationMethod.ignore,
                                        });
                                },
                                () => throw new Exception("Guid not unique."),
                                async (existingConnector) =>
                                {
                                    adapterInternal.connectorIds = adapterInternal.connectorIds.Append(connectorId).ToArray();
                                    await saveAdapterInternalAsync(adapterInternal);
                                    adapterExternal.connectorIds = adapterExternal.connectorIds.Append(connectorId).ToArray();
                                    await saveAdapterExternalAsync(adapterExternal);

                                    return onSuccess(existingConnector);
                                });
                        }));
        }

        //public async Task<TResult> ResolveResourcesAsync<TResourceInternal, TResult>(
        //        TResourceInternal[] resourcesInternal, IEnumerable<Adapter> resourcesExternal,
        //        Guid actorId, Guid integrationIdInternal, Guid integrationIdExternal, string resourceType,
        //        Func<TResourceInternal, Adapter> getIdentifiersInternal,
        //    Func<IEnumerable<Connection>, TResult> onMatch,
        //    Func<string, TResult> onFailure)
        //    where TResourceInternal : struct
        //{
        //    return await resourcesInternal
        //        .FlatMap(
        //            resourcesExternal.ToDictionary(res => res.key, res => res),
        //            async (resourceInternal, resourcesExternalUnmatched, next, skip) =>
        //            {
        //                var adapterInternal = getIdentifiersInternal(resourceInternal);
        //                if (adapterInternal.key.IsNullOrWhiteSpace())
        //                    throw new ArgumentException("getIdentifiersInternal must return a synchronization that has a valid internal id", "getIdentifiersInternal");

        //                var resourceKey = adapterInternal.key;
        //                return await Persistence.AdapterDocument.FindOrCreateAsync(resourceKey,
        //                        integrationIdInternal, resourceType, integrationIdExternal,
        //                    async (adapterInternalStorage, connectorAdapter, updateAsync) =>
        //                    {
        //                        var adapterLocal = new Adapter()
        //                        {
        //                            key = resourceKey,
        //                            name = adapterInternal.name,
        //                            adapterId = adapterInternalStorage.adapterId,
        //                            identifiers = adapterInternal.identifiers,
        //                            integrationId = integrationIdInternal,
        //                        };
        //                        if (!connectorAdapter.HasValue)
        //                        {
        //                            // Update identifiers internally if there is no externally mapped resource
        //                            var connection = await updateAsync(adapterLocal, default(KeyValuePair<Connector, Adapter>?));
        //                            return await next(connection, resourcesExternalUnmatched);
        //                        }
        //                        var adapterRemote = connectorAdapter.Value.Value;
        //                        var connector = connectorAdapter.Value.Key;

        //                        var remoteKey = adapterRemote.key;
        //                        if (!resourcesExternalUnmatched.ContainsKey(remoteKey))
        //                        {
        //                            bool deleted = await Persistence.AdapterDocument.DeleteByIdAsync(adapterRemote.adapterId,
        //                                () => true,
        //                                () => false);
        //                            var connectionMissing = await updateAsync(adapterLocal, default(KeyValuePair<Connector, Adapter>?));
        //                            return await next(connectionMissing, resourcesExternalUnmatched);
        //                        }

        //                        var adapterExternal = resourcesExternalUnmatched[remoteKey];
        //                        var connectionUpdated = await updateAsync(adapterLocal, connector.PairWithValue(adapterExternal));
        //                        return await next(connectionUpdated, resourcesExternalUnmatched.Where(kvp => kvp.Key != remoteKey).ToDictionary());
        //                    },
        //                    async (saveLinkAsync) =>
        //                    {
        //                        var adapterToCreate = new Adapter()
        //                        {
        //                            adapterId = Guid.NewGuid(),
        //                            key = resourceKey,
        //                            name = adapterInternal.name,
        //                            identifiers = adapterInternal.identifiers,
        //                            integrationId = integrationIdInternal,
        //                        };
        //                        var connection = await saveLinkAsync(adapterToCreate, default(KeyValuePair<Connector, Adapter>?));
        //                        return await next(connection, resourcesExternalUnmatched);
        //                    });
        //            },
        //            // Add in the unmatched synchronization
        //            async (Connection[] synchronizationsInternalOrMatched, IDictionary<string, Adapter> resourcesExternalUnmatched) =>
        //            {
        //                var connections = await resourcesExternalUnmatched
        //                    .FlatMap(
        //                        async (resourceKvp, next, skip) =>
        //                        {
        //                            var resourceKey = resourceKvp.Key;
        //                            var adapter = resourceKvp.Value;
        //                            return await Persistence.AdapterDocument.FindOrCreateAsync(resourceKey,
        //                                integrationIdExternal, resourceType, integrationIdExternal,
        //                                async (adapterExternalStorage, connector, updateAsync) =>
        //                                {
        //                                    var adapterUpdated = new Adapter()
        //                                    {
        //                                        adapterId = adapterExternalStorage.adapterId,
        //                                        key = resourceKey,
        //                                        name = adapter.name,
        //                                        identifiers = adapter.identifiers,
        //                                        integrationId = integrationIdExternal,
        //                                    };
        //                                    var externalAdapter = resourceKvp.Value;

        //                                    // It may seem like this would not cause any changes but it's updating identifiers
        //                                    var connection = await updateAsync(adapterUpdated, default(KeyValuePair<Connector, Adapter>?));
        //                                    return await next(connection);
        //                                },
        //                                async (saveLinkAsync) =>
        //                                {
        //                                    var adapterToCreate = new Adapter()
        //                                    {
        //                                        adapterId = Guid.NewGuid(),
        //                                        key = resourceKey,
        //                                        name = adapter.name,
        //                                        identifiers = adapter.identifiers,
        //                                        integrationId = integrationIdInternal,
        //                                    };

        //                                    var connection = await saveLinkAsync(adapterToCreate, default(KeyValuePair<Connector, Adapter>?));
        //                                    return await next(connection);
        //                                });
        //                        },
        //                        (IEnumerable<Connection> synchronizationsFromExternal) =>
        //                        {
        //                            return synchronizationsFromExternal
        //                                .Concat(synchronizationsInternalOrMatched)
        //                                .ToArray()
        //                                .ToTask();
        //                        });
        //                return onMatch(connections);
        //            });
        //}

        public static Task<TResult> FindAdapterByIdAsync<TResult>(Guid synchronizationId,
            Func<EastFive.Azure.Synchronization.Adapter, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return EastFive.Azure.Synchronization.Persistence.AdapterDocument.FindByIdAsync(synchronizationId,
                (synchronization) => onFound(synchronization),
                () => onNotFound());
        }

        //protected async Task<TResult> GetSynchronizationsAsync<TResourceInternal, TResult>(
        //        TResourceInternal[] resourcesInternal, ResourceAllSynchronizationsAsync<Task<TResult>> fetchAllAsync,
        //        Guid actorId, Guid integrationIdInternal, Guid integrationIdExternal, string resourceType,
        //        Func<TResourceInternal, Adapter> getAdapterInternal,
        //    Func<IEnumerable<Connection>, Task<TResult>> onMatch,
        //    Func<string, Task<TResult>> onFailure)
        //    where TResourceInternal : struct
        //{
        //    return await await fetchAllAsync(
        //        async (resourcesExternal) =>
        //        {
        //            return await await ResolveResourcesAsync(resourcesInternal, resourcesExternal,
        //                    actorId, integrationIdInternal, integrationIdExternal, resourceType,
        //                    getAdapterInternal,
        //                (synchronizations) => onMatch(synchronizations),
        //                onFailure);
        //        },
        //        onFailure,
        //        onFailure,
        //        () => onFailure("Not supported"));
        //}
        
        public static async Task<TResult> FindAdaptersByRelatedAsync<TResult>(Guid relatedAdapterId, Guid integrationId,
                System.Security.Claims.Claim[] claims,
            Func<Adapter[], TResult> onFound,
            Func<TResult> onReferenceNotFound,
            Func<TResult> onUnauthorized)
        {
            return await await Persistence.AdapterDocument.FindByIdAsync(relatedAdapterId,
                async relatedAdapter =>
                {
                    var orderedAdapters = await Persistence.AdapterDocument
                        .FindAll(integrationId, relatedAdapter.resourceType)
                        .OrderByAsync(adapter => relatedAdapter.name.SmithWaterman(adapter.name));
                    return onFound(orderedAdapters.ToArray());
                },
                onReferenceNotFound.AsAsyncFunc());
        }

        public static Task<TResult> FindAdapterByKeyAsync<TResult>(string key, Guid integrationId, string resourceType,
                System.Security.Claims.Claim[] claims,
            Func<Adapter, TResult> onFound,
            Func<TResult> onReferenceNotFound,
            Func<TResult> onUnauthorized)
        {
            return FindAdapterByKeyAsync(key, integrationId, resourceType,
                onFound,
                onReferenceNotFound);
        }

        public static async Task<TResult> FindAdapterByKeyAsync<TResult>(string key, Guid integrationId, string resourceType,
            Func<Adapter, TResult> onFound,
            Func<TResult> onReferenceNotFound)
        {
            return await Persistence.AdapterDocument.FindByKeyAsync(key, integrationId, resourceType,
                relatedAdapter =>
                {
                    return onFound(relatedAdapter);
                },
                onReferenceNotFound);
        }

        public static async Task<TResult> FindAdapterConnectorByKeyAsync<TResult>(string localResourceKey, Guid localIntegrationId, string localResourceType,
                Guid remoteIntegrationId,
            Func<Connector, Adapter, TResult> onFound,
            Func<TResult> onLocalAdapterNotFound)
        {
            return await await FindAdapterByKeyAsync(localResourceKey, localIntegrationId, localResourceType,
                (localAdapter) => localAdapter.connectorIds
                    .SelectAsyncOptional<Guid, KeyValuePair<Adapter, Connector>>(
                        (connectorId, select, skip) => Persistence.ConnectorDocument.FindByIdWithAdapterRemoteAsync(connectorId, localAdapter,
                            (remoteConnector, remoteAdapter) => select(remoteAdapter.PairWithValue(remoteConnector)),
                            skip))
                   .FirstMatchAsync<KeyValuePair<Adapter, Connector>, TResult>(
                        (adapterConnectorKvp, next) =>
                        {
                            var remoteAdapter = adapterConnectorKvp.Key;
                            var remoteConnector = adapterConnectorKvp.Value;
                            if (remoteAdapter.integrationId == remoteIntegrationId)
                                return onFound(remoteConnector, remoteAdapter);
                            return next();
                        },
                        () => onLocalAdapterNotFound()),
                onLocalAdapterNotFound.AsAsyncFunc());
        }

        public static Task<TResult> FindAdapterConnectorsByKeyAsync<TResult>(string localResourceKey, Guid localIntegrationId, string localResourceType,
            Func<IEnumerableAsync<KeyValuePair<Connector, Adapter>>, TResult> onFound,
            Func<TResult> onLocalAdapterNotFound)
        {
            return FindAdapterByKeyAsync(localResourceKey, localIntegrationId, localResourceType,
                (localAdapter) =>
                {
                    var adapterConnectorKpvs = localAdapter.connectorIds
                        .SelectAsyncOptional<Guid, KeyValuePair<Connector, Adapter>>(
                            (connectorId, select, skip) => Persistence.ConnectorDocument.FindByIdWithAdapterRemoteAsync(connectorId, localAdapter,
                                (remoteConnector, remoteAdapter) => select(remoteAdapter.PairWithKey(remoteConnector)),
                                skip));
                    return onFound(adapterConnectorKpvs);
                },
                onLocalAdapterNotFound);
        }

        public abstract Task<TResult> GetAdaptersAsync<TResult>(
            Guid integrationId,
            Func<IEnumerable<Adapter>, TResult> onMatch,
            Func<string, TResult> onFailure);

        public abstract Task<TResult> DeleteAsync<TResult>(
                string resourceType, string resourceKey,
            Func<TResult> onSuccess,
            Func<string, TResult> onFailure);

        #region Convenience methods
        
        public static Task<TResult> FindInternalAdapterConnectorByExternalKeyAsync<TResult>(string key, Guid integrationId, string resourceType,
            Func<Connector, Adapter, TResult> onFound,
            Func<TResult> onReferenceNotFound)
        {
            return FindAdapterConnectorByKeyAsync(key, integrationId, resourceType, defaultInternalIntegrationId,
                onFound,
                onReferenceNotFound);
        }

        /// <summary>
        /// Convenience method for creating an adapter for an "internal" and external resource, and a connector for the two adapters.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="resourceIdInternal"></param>
        /// <param name="resourceIdExternalSystem"></param>
        /// <param name="externalSystemIntegrationId"></param>
        /// <param name="resourceType"></param>
        /// <param name="onSuccess"></param>
        /// <returns></returns>
        public static Task<TResult> CreateOrUpdateInternalExternalConnection<TResult>(Guid resourceIdInternal,
            string resourceKeyExternal, Guid externalSystemIntegrationId,
            string resourceType,
            Func<Guid, TResult> onSuccess)
        {
            return CreateOrUpdateInternalExternalConnection(resourceIdInternal.ToString("N"), resourceKeyExternal, externalSystemIntegrationId, resourceType, onSuccess);
        }

        public static Task<TResult> CreateOrUpdateConnection<TResult>(Guid resourceIdInternal,
            string resourceKeyExternal, Guid externalSystemIntegrationId,
            string resourceType,
            Func<Connector, TResult> onSuccess)
        {
            return CreateOrUpdateInternalExternalConnection(resourceIdInternal.ToString("N"), resourceKeyExternal, externalSystemIntegrationId, resourceType, onSuccess);
        }

        public static Task<TResult> CreateOrUpdateInternalExternalConnection<TResult>(string resourceKeyInternal,
            string resourceKeyExternal, Guid externalSystemIntegrationId,
            string resourceType,
            Func<Guid, TResult> onSuccess)
        {
            return CreateOrUpdateInternalExternalConnection(resourceKeyInternal,
                    resourceKeyExternal, externalSystemIntegrationId,
                    resourceType,
                (Connector connector) => onSuccess(connector.connectorId));
        }

        public static Task<TResult> CreateOrUpdateInternalExternalConnection<TResult>(string resourceKeyInternal,
            string resourceKeyExternal, Guid externalSystemIntegrationId,
            string resourceType,
            Func<Connector, TResult> onSuccess)
        {
            return CreateOrUpdateConnection(resourceKeyInternal, defaultInternalIntegrationId,
                resourceKeyExternal, externalSystemIntegrationId,
                resourceType,
                onSuccess);
        }

        public static IEnumerableAsync<Guid> CreateOrReplaceBatchConnection(
            IEnumerableAsync<KeyValuePair<Guid, string>> resourceIdKeys,
            Guid externalSystemIntegrationId, string resourceType,
            Connector.SynchronizationMethod method = Connector.SynchronizationMethod.ignore)
        {
            // Pull this into a batch in case there are any misses in lining up adapters and connectors

            return resourceIdKeys
                .Batch()
                .Select(
                    batchResourceIdKeys => CreateOrReplaceBatchWrappedConnection(
                        batchResourceIdKeys,
                        externalSystemIntegrationId, resourceType, method))
                .Await()
                .SelectMany();
        }

        private static async Task<Guid[]> CreateOrReplaceBatchWrappedConnection(
            KeyValuePair<Guid, string>[] resourceIdKeys,
            Guid externalSystemIntegrationId, string resourceType,
            Connector.SynchronizationMethod method)
        {
            var adaptersInternalTask = Persistence.AdapterDocument
                .CreateOrUpdateBatch(
                    resourceIdKeys
                        .Distinct(resourceIdKey => resourceIdKey.Key)
                        .Select(resourceIdKey => resourceIdKey.Key.ToString("N")),
                    defaultInternalIntegrationId, resourceType)
                .ToArrayAsync(); //.ToDictionary(adapter => Guid.Parse(adapter.key), adapter => adapter.adapterId);

            var adaptersExternalTask = Persistence.AdapterDocument
                .CreateOrUpdateBatch(
                    resourceIdKeys
                        .Distinct(resourceIdKey => resourceIdKey.Value)
                        .Select(resourceIdKey => resourceIdKey.Value),
                    defaultInternalIntegrationId, resourceType)
                .ToArrayAsync(); // (adapter => adapter.key, adapter => adapter.adapterId);

            var adaptersInternal = (await adaptersInternalTask)
                .ToDictionary(
                    adapter => adapter.key,
                    adapter => adapter.adapterId);
            var adaptersExternal = (await adaptersExternalTask)
                .ToDictionary(
                    adapter => adapter.key,
                    adapter => adapter.adapterId);
            var connectors = resourceIdKeys
                .Where(resourceIdKey => adaptersInternal.ContainsKey(resourceIdKey.Key.ToString("N")))
                .Where(resourceIdKey => adaptersExternal.ContainsKey(resourceIdKey.Value))
                .Select(
                    (resourceIdKey) =>
                    {
                        var adapterIdInternal = adaptersInternal[resourceIdKey.Key.ToString("N")];
                        var adapterIdExternal = adaptersExternal[resourceIdKey.Value];
                        var connector = new Connector
                        {
                            connectorId = Guid.NewGuid(),
                            adapterInternalId = adapterIdInternal,
                            adapterExternalId = adapterIdExternal,
                            synchronizationMethod = method,
                        };
                        return connector;
                    });

            return await Persistence.ConnectorDocument.CreateBatch(connectors, method)
                .ToArrayAsync();
        }

        
        public static IEnumerableAsync<Adapter> FindAdaptersByType(string resourceType)
        {
            var adapters = EastFive.Azure.Synchronization.Persistence.AdapterDocument.FindAll(resourceType);
            return adapters;
        }
        
        public static IEnumerableAsync<Connection> FindConnectionsByType(string resourceType)
        {
            var adapters = Persistence.ConnectorDocument.FindAllByType(resourceType);
            return adapters;
        }

        /// <summary>
        /// Convenience method for looking up an external to "internal"/default resource mapping.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="keyGuid"></param>
        /// <param name="integrationId"></param>
        /// <param name="resourceType"></param>
        /// <param name="onFound"></param>
        /// <param name="onReferenceNotFound"></param>
        /// <returns></returns>
        public static async Task<TResult> FindInternalIdByResourceKeyAsync<TResult>(string key, Guid integrationId, string resourceType,
            Func<Guid, TResult> onFound,
            Func<TResult> onConnectionNotFound)
        {
            return await await Persistence.AdapterDocument.FindByKeyAsync(key, integrationId, resourceType,
                adapterInternal =>
                {
                    return Persistence.ConnectorDocument.FindByAdapterWithConnectionAsync(adapterInternal,
                        (KeyValuePair<Connector, Adapter>[] connectorAdapterExternalIdKvps) =>
                        {
                            return connectorAdapterExternalIdKvps.First(
                                (connectorAdapterExternalIdKvp, next) =>
                                {
                                    if (connectorAdapterExternalIdKvp.Value.integrationId == defaultInternalIntegrationId)
                                        if(Guid.TryParse(connectorAdapterExternalIdKvp.Value.key, out Guid internalId))
                                            return onFound(internalId);

                                    return next();
                                },
                                onConnectionNotFound);
                        },
                        onConnectionNotFound);
                },
                onConnectionNotFound.AsAsyncFunc());
        }

        /// <summary>
        /// Convenience method for looking up an "internal" to external resource mapping.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="keyGuid"></param>
        /// <param name="integrationId"></param>
        /// <param name="resourceType"></param>
        /// <param name="onFound"></param>
        /// <param name="onReferenceNotFound"></param>
        /// <returns></returns>
        public static Task<TResult> FindResourceKeyByInternalIdAsync<TResult>(Guid keyGuid, Guid integrationId, string resourceType,
            Func<string, TResult> onFound,
            Func<TResult> onConnectionNotFound)
        {
            var key = keyGuid.ToString("N");
            return FindResourceKeyByInternalKeyAsync<TResult>(key, integrationId, resourceType, onFound, onConnectionNotFound);
        }

        public static async Task<TResult> FindResourceKeyByInternalKeyAsync<TResult>(string keyInternal,
                Guid integrationId, string resourceType,
            Func<string, TResult> onFound,
            Func<TResult> onConnectionNotFound)
        {
            return await await FindResourceKeysByInternalKeyAsync(keyInternal, resourceType,
                integrationIdResourceKeys =>
                {
                    return integrationIdResourceKeys
                        .FirstAsyncMatchAsync(
                            async (integrationIdResourceKey, next) =>
                            {
                                var externalAdapter = integrationIdResourceKey.Value;
                                var isAdapterInDesiredIntegration = externalAdapter.integrationId == integrationId;
                                var doesAdapterHaveKeyValue = !externalAdapter.key.IsNullOrWhiteSpace();
                                var useThisAdapter = isAdapterInDesiredIntegration && doesAdapterHaveKeyValue;
                                if (useThisAdapter)
                                    return onFound(externalAdapter.key);

                                return await next();
                            },
                            () => onConnectionNotFound());
                },
                onConnectionNotFound.AsAsyncFunc());
        }

        public static Task<TResult> FindResourceKeysByInternalIdAsync<TResult>(Guid keyGuid, string resourceType,
            Func<IEnumerableAsync<KeyValuePair<Connector, Adapter>>, TResult> onFound,
            Func<TResult> onAdapterNotFound)
        {
            var key = keyGuid.ToString("N");
            return FindResourceKeysByInternalKeyAsync(key, resourceType, onFound, onAdapterNotFound);
        }

        public static Task<TResult> FindResourceKeysByInternalKeyAsync<TResult>(string keyInternal, string resourceType,
            Func<IEnumerableAsync<KeyValuePair<Connector, Adapter>>, TResult> onFound,
            Func<TResult> onAdapterNotFound)
        {
            return Persistence.AdapterDocument.FindByKeyAsync<TResult>(keyInternal, defaultInternalIntegrationId, resourceType,
                adapterInternal =>
                {
                    var connectorAdapterKvps = adapterInternal.connectorIds
                        .SelectAsyncOptional<Guid, KeyValuePair<Connector, Adapter>>(
                            (connectorId, select, skip) => Persistence.ConnectorDocument.FindByIdWithAdapterRemoteAsync(connectorId, adapterInternal,
                                (connector, adapter) => select(connector.PairWithValue(adapter)),
                                skip));
                    return onFound(connectorAdapterKvps);
                },
                () =>
                {
                    return onAdapterNotFound();
                });
        }
        
        public static Task<TResult> FindAdapterConnectorsByInternalIdAsync<TResult>(Guid internalGuidKey, string resourceType,
            Func<IEnumerableAsync<KeyValuePair<Connector, Adapter>>, TResult> onFoundInternalAdapter,
            Func<TResult> onInternalAdapterNotFound)
        {
            var internalKey = internalGuidKey.ToString("N");
            return FindAdapterConnectorsByKeyAsync(internalKey, defaultInternalIntegrationId, resourceType,
                onFoundInternalAdapter,
                onInternalAdapterNotFound);
        }

        public static Task<TResult> FindAdapterConnectorByInternalIdAsync<TResult>(Guid internalGuidKey, string resourceType, Guid remoteIntegrationId,
            Func<Connector, Adapter, TResult> onFoundInternalAdapter,
            Func<TResult> onInternalAdapterNotFound)
        {
            var internalKey = internalGuidKey.ToString("N");
            return FindAdapterConnectorByKeyAsync(internalKey, defaultInternalIntegrationId, resourceType, remoteIntegrationId,
                onFoundInternalAdapter,
                onInternalAdapterNotFound);
        }

        #endregion

        public static Task<TResult> SynchronizeLockedAsync<TResult>(Guid connectorId, string resourceType,
            Func<TimeSpan?, 
                Func<TResult, Task<Persistence.ConnectorDocument.ILockResult<TResult>>>, 
                Func<TResult, Task<Persistence.ConnectorDocument.ILockResult<TResult>>>,
                Task<Persistence.ConnectorDocument.ILockResult<TResult>>> onLockAquired,
            Func<int, 
                TimeSpan,
                TimeSpan?,
                Func<Task<TResult>>, 
                Func<Task<TResult>>,
                Task<TResult>> onAlreadyLocked,
            Func<TResult> onNotFound)
        {
            return Persistence.ConnectorDocument.SynchronizeLockedAsync(connectorId, resourceType,
                async (duration, unlockAndSave, unlock) =>
                {
                    var result = await onLockAquired(duration,
                        (t) => unlockAndSave(t),
                        (t) => unlock(t));
                    return result;
                },
                onAlreadyLocked:
                    (retryCount, retrySpan, doc, continueAquiring, force) =>
                    {
                        return onAlreadyLocked(retryCount, retrySpan, doc, continueAquiring, force);
                    },
                onNotFound:onNotFound);
        }

        public static async Task<TResult> DeleteAdapterAndConnections<TResult>(Adapter adapter, 
            Func<TResult> onDeleted,
            Func<string, TResult> onFailure)
        {
            bool [] successes = await adapter.connectorIds
                .Select(
                    async connectorId =>
                    {
                        bool deleted = await await Persistence.ConnectorDocument.DeleteByIdAsync(connectorId,
                            (adapterId1, adapterId2) =>
                            {
                                var otherAdapterId = adapterId1 == adapter.adapterId ?
                                    adapterId2
                                    :
                                    adapterId1;
                                return Persistence.AdapterDocument.UpdateAsync(otherAdapterId,
                                    async (otherAdapter, updateOtherAdapterAsync) =>
                                    {
                                        await updateOtherAdapterAsync(otherAdapter.connectorIds.Where(cId => cId != connectorId).ToArray(), otherAdapter.name, otherAdapter.identifiers);
                                        return true;
                                    },
                                    () => false);
                            },
                            () => false.AsTask());
                        return deleted;
                    })
                .AsyncEnumerable()
                .JoinTask(Persistence.AdapterDocument.DeleteByIdAsync(adapter.adapterId,
                    () => true,
                    () => false))
                .ToArrayAsync();
            return onDeleted();

        }
    }
}

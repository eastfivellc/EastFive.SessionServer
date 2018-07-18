using BlackBarLabs.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Synchronization
{
    public static class Connectors
    {
        public static async Task<TResult> CreateConnectorAsync<TResult>(Guid connectorId, Guid sourceId, Guid? destinationAdapterIdMaybe,
                Connector.SynchronizationMethod flow, Guid? destinationIntegrationMaybe,
                Guid performingAsActorId, Claim[] claims,
            Func<TResult> onCreated,
            Func<Connection, TResult> onCreatedAndModified,
            Func<TResult> onAlreadyExists,
            Func<Guid, TResult> onRelationshipAlreadyExists,
            Func<Guid, TResult> onAdapterDoesNotExist,
            Func<string, TResult> onFailure)
        {
            return await await Persistence.AdapterDocument.FindByIdAsync(sourceId,
                async sourceAdapter =>
                {
                    Func<Guid, Adapter?, Task<TResult>> createConnector =
                        async (destinationAdapterId, destinationAdapterMaybe) => await await Persistence.ConnectorDocument.CreateAsync(connectorId,
                            sourceId, destinationAdapterId, flow,
                            async () =>
                            {
                                if (!destinationAdapterMaybe.HasValue)
                                    return await onCreated().ToTask();

                                var connector = new Connector()
                                {
                                    connectorId = connectorId,
                                    adapterExternalId = destinationAdapterId,
                                    adapterInternalId = sourceId,
                                    createdBy = sourceId,
                                    synchronizationMethod = flow,
                                };
                                var connection = Convert(sourceAdapter, connector, destinationAdapterMaybe.Value);
                                return onCreatedAndModified(connection);
                            },
                            onAlreadyExists.AsAsyncFunc(),
                            async (callback) => onRelationshipAlreadyExists(await callback()),
                            onAdapterDoesNotExist.AsAsyncFunc());

                    if (destinationAdapterIdMaybe.HasValue)
                        return await createConnector(destinationAdapterIdMaybe.Value, default(Adapter?));

                    if (!destinationIntegrationMaybe.HasValue)
                        return onFailure("Destination integration must be specified to create a new destination.");
                    return await Persistence.AdapterDocument.FindOrCreateAsync($"TODO:{Guid.NewGuid()}", destinationIntegrationMaybe.Value, sourceAdapter.resourceType,
                            async (created, destinationAdapter, saveAsync) =>
                            {
                                if (!created)
                                    return onAlreadyExists();
                                var destinationAdapterId = await saveAsync(destinationAdapter);
                                return await createConnector(destinationAdapterId, destinationAdapter);
                            });
                },
                () => onAdapterDoesNotExist(sourceId).ToTask());
            
        }

        public static async Task<TResult> UpdateConnectorAsync<TResult>(Guid connectorId,
                Connector.SynchronizationMethod flow,
                Guid performingAsActorId, Claim[] claims,
            Func<TResult> onCreated,
            Func<TResult> onAdapterDoesNotExist,
            Func<string, TResult> onFailure)
        {
            return await Persistence.ConnectorDocument.UpdateAsync(connectorId,
                async (connector, saveAsync) =>
                {
                    await saveAsync(flow);
                    return onCreated();
                },
                onAdapterDoesNotExist);
        }

        public static Task<TResult> FindByIdAsync<TResult>(Guid connectorId,
                Guid performingAsAuthorization, System.Security.Claims.Claim[] claims,
            Func<Connector, Guid, TResult> onFound,
            Func<TResult> onNotFound,
            Func<TResult> onUnauthorized)
        {
            return Persistence.ConnectorDocument.FindByIdAsync(connectorId,
                (connector, destinationIntegrationId) =>
                {
                    return onFound(connector, destinationIntegrationId);
                },
                onNotFound);
        }

        public static Task<TResult> FindByAdapterAsync<TResult>(Guid adapterId,
                Guid performingAsAuthorization, System.Security.Claims.Claim[] claims,
            Func<KeyValuePair<Connector, Guid>[], TResult> onFound,
            Func<TResult> onAdapterNotFound,
            Func<TResult> onUnauthorized)
        {
            return Persistence.ConnectorDocument.FindByAdapterAsync(adapterId,
                (adapter, connectors) =>
                {
                    return onFound(connectors);
                },
                onAdapterNotFound);
        }

        public static Task<TResult> FindConnectionByAdapterAsync<TResult>(Guid adapterId,
                Guid performingAsAuthorization, System.Security.Claims.Claim[] claims,
            Func<Connection[], TResult> onFound,
            Func<TResult> onAdapterNotFound,
            Func<TResult> onUnauthorized)
        {
            return Persistence.ConnectorDocument.FindByAdapterWithConnectionAsync(adapterId,
                (adapter, connectorAdapters) =>
                {
                    return onFound(
                        connectorAdapters
                            .Select(
                                connectorAdapter => 
                                    Convert(adapter, connectorAdapter.Key, connectorAdapter.Value))
                            .ToArray());
                },
                onAdapterNotFound);
        }

        private static Connection Convert(Adapter source, Connector connector, Adapter remote)
        {
            return new Connection()
            {
                connector = new Connector()
                {
                    connectorId = connector.connectorId,
                    adapterInternalId = source.adapterId,
                    adapterExternalId = connector.adapterInternalId == source.adapterId ?
                                            connector.adapterExternalId
                                            :
                                            connector.adapterInternalId,
                    synchronizationMethod = connector.adapterInternalId == source.adapterId ?
                                            connector.synchronizationMethod
                                            :
                                            connector.synchronizationMethod == Connector.SynchronizationMethod.useExternal ?
                                                Connector.SynchronizationMethod.useInternal
                                                :
                                                connector.synchronizationMethod == Connector.SynchronizationMethod.useInternal ?
                                                    Connector.SynchronizationMethod.useExternal
                                                    :
                                                    connector.synchronizationMethod,
                    createdBy = connector.createdBy,
                },
                adapterInternal = source,
                adapterExternal = remote,
            };
        }

        public static Task<TResult> DeleteByIdAsync<TResult>(Guid connectorId,
                Guid performingAsAuthorization, System.Security.Claims.Claim[] claims,
            Func<TResult> onDeleted,
            Func<TResult> onNotFound)
        {
            return Persistence.ConnectorDocument.DeleteByIdAsync(connectorId,
                (localAdapter, remoteAdapter) =>
                {
                    return onDeleted();
                    //Synchronizations synchronizations;
                    //if ( !synchronizations..ContainsKey(resourceType))
                    //    return localId.PairWithValue(remoteId).PairWithValue($"Type [{resourceType}] is not supported by this integration");

                    //return await await this.resources[resourceType].DeleteAsync(service, remoteId,
                    //    async () =>
                    //    {
                    //        await deleteAsync();
                    //        return localId.PairWithValue(remoteId).PairWithValue(resourceType);
                    //    },
                    //    async () =>
                    //    {
                    //        await deleteAsync();
                    //        return localId.PairWithValue(remoteId).PairWithValue($"{resourceType}:None found");
                    //    },
                    //    async () =>
                    //    {
                    //        await deleteAsync();
                    //        return localId.PairWithValue(remoteId).PairWithValue($"{resourceType}:Delete not supported");
                    //    },
                    //    (why) => localId.PairWithValue(remoteId).PairWithValue($"{resourceType}:{why}").ToTask());
                    //return onDeleted();
                },
                onNotFound);
        }
    }
}

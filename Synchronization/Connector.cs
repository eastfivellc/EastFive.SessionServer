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
        public static async Task<TResult> CreateConnectorAsync<TResult>(Guid connectorId, Guid sourceId, Guid? destination,
                Connector.SynchronizationMethod flow, Guid createdBy,
                Guid performingAsActorId, Claim[] claims,
            Func<TResult> onCreated,
            Func<TResult> onAlreadyExists,
            Func<Guid, TResult> onAdapterDoesNotExist,
            Func<string, TResult> onFailure)
        {
            if (!destination.HasValue)
            {
                //EastFive.Azure.Synchronization.A
            }
            return await Persistence.ConnectorDocument.CreateAsync(connectorId,
                    sourceId, destination.Value, createdBy, flow,
                () =>
                {

                    return onCreated();
                },
                onAlreadyExists,
                onAdapterDoesNotExist);
        }

        public static Task<TResult> FindByIdAsync<TResult>(Guid connectorId,
                Guid performingAsAuthorization, System.Security.Claims.Claim[] claims,
            Func<Connector, TResult> onFound,
            Func<TResult> onNotFound,
            Func<TResult> onUnauthorized)
        {
            return Persistence.ConnectorDocument.FindByIdAsync(connectorId,
                (connector) =>
                {
                    return onFound(connector);
                },
                onNotFound);
        }

        public static Task<TResult> FindByAdapterAsync<TResult>(Guid adapterId,
                Guid performingAsAuthorization, System.Security.Claims.Claim[] claims,
            Func<Connector[], TResult> onFound,
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
                                connectorAdapter => new Connection()
                                {
                                    connector = new Connector()
                                    {
                                        connectorId = connectorAdapter.Key.connectorId,
                                        adapterInternalId = adapter.adapterId,
                                        adapterExternalId = connectorAdapter.Key.adapterInternalId == adapter.adapterId?
                                            connectorAdapter.Key.adapterExternalId
                                            :
                                            connectorAdapter.Key.adapterInternalId,
                                        synchronizationMethod = connectorAdapter.Key.adapterInternalId == adapter.adapterId ?
                                            connectorAdapter.Key.synchronizationMethod
                                            :
                                            connectorAdapter.Key.synchronizationMethod == Connector.SynchronizationMethod.useExternal? 
                                                Connector.SynchronizationMethod.useInternal
                                                :
                                                connectorAdapter.Key.synchronizationMethod == Connector.SynchronizationMethod.useInternal?
                                                    Connector.SynchronizationMethod.useExternal
                                                    :
                                                    connectorAdapter.Key.synchronizationMethod,
                                        createdBy = connectorAdapter.Key.createdBy,
                                    },
                                    adapterInternal = adapter,
                                    adapterExternal = connectorAdapter.Value,
                                })
                            .ToArray());
                },
                onAdapterNotFound);
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

using BlackBarLabs;
using BlackBarLabs.Collections.Generic;
using BlackBarLabs.Extensions;
using BlackBarLabs.Linq.Async;
using EastFive;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Security.SessionServer;
using EastFive.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Synchronization
{
    public class Synchronizations<IIntegrate>
        where IIntegrate: class
    {
        private Context context;
        private IDictionary<string, Func<IIntegrate, Connections>> connections;

        public Synchronizations(Context context, IEnumerable<Func<IIntegrate, Connections>> connectionHandlers)
        {
            this.context = context;
            this.connections = connectionHandlers.ToDictionary(connectionHandler => connectionHandler((IIntegrate)null).ResourceType);
        }

        internal async Task<TResult> GetServicesAsync<TResult>(Guid actorId,
            Func<KeyValuePair<EastFive.Api.Azure.Integration, IIntegrate>[], Task<TResult>> onSuccess)
        {
            var integrations = await context.Integrations.GetActivitiesAsync<IIntegrate>(actorId);
            return await onSuccess(integrations);
        }

        public Task<TResult> ImportByActorServiceAndResourceAsync<TResult>(Guid actorId,
                string serviceName, string resourceType,
                Guid actorPerformingAs, System.Security.Claims.Claim[] claims,
            Func<EastFive.Azure.Synchronization.Connection[], TResult> onFound,
            Func<TResult> onUnsupportedResourceType,
            Func<TResult> onReferenceNotFound,
            Func<TResult> onUnauthorized)
        {
            // Get all external items
            return GetServicesAsync(actorId,
                (serviceKvps) =>
                {
                    return serviceKvps.SelectReduce<KeyValuePair<EastFive.Api.Azure.Integration, IIntegrate>, EastFive.Azure.Synchronization.Connection[], Task<TResult>>(
                        async (serviceKvp, next) =>
                        {
                            var service = serviceKvp.Value;
                            if (resourceType.IsNullOrWhiteSpace() || (!connections.ContainsKey(resourceType)))
                            {
                                if (resourceType.IsNullOrWhiteSpace() || (!connections.ContainsKey(resourceType)))
                                    return onUnsupportedResourceType();

                                var connection = connections[resourceType](service);
                                return await await connection.SynchronizeAsync(
                                        actorId, actorId, serviceKvp.Key.integrationId,
                                    (synchronizations) => next(synchronizations.ToArray()),
                                    (why) => next(new EastFive.Azure.Synchronization.Connection[] { }));
                            }
                            return await next(new EastFive.Azure.Synchronization.Connection[] { });
                            //var resource = resources[resourceType];
                            //return await await resource.SynchronizeAsync(
                            //        actorId, service, serviceKvp.Key,
                            //    (synchronizations) => next(synchronizations.ToArray()),
                            //    (why) => next(new EastFive.Azure.Synchronization.Connection[] { }));
                        },
                        (synchronizationss) =>
                        {
                            return onFound(synchronizationss.SelectMany().ToArray()).ToTask();
                        });
                });
        }
        
        public async Task<TResult> FindAdaptersByIntgrationAndResourceTypeAsync<TResult>(Guid integrationId, string resourceType,
                Guid actorPerformingAs, System.Security.Claims.Claim[] claims,
            Func<EastFive.Azure.Synchronization.Adapter[], TResult> onFound,
            Func<TResult> onNotFound,
            Func<TResult> onUnsupportedResourceType,
            Func<TResult> onUnauthorizedIntegration,
            Func<TResult> onUnauthorized,
            Func<string, TResult> onFailure)
        {
            if (resourceType.IsNullOrWhiteSpace() || (!connections.ContainsKey(resourceType)))
                return onUnsupportedResourceType();

            return await await context.Integrations.GetByIdAsync(integrationId,
                async (actorIdMaybe, method) =>
                {
                    if (!actorIdMaybe.HasValue)
                        return onUnauthorizedIntegration();

                    var actorId = actorIdMaybe.Value;
                    // Get all external items
                    return await GetServicesAsync(actorId,
                        (serviceKvps) =>
                        {
                            return serviceKvps.First(
                                async (serviceKvp, next) =>
                                {
                                    if (serviceKvp.Key.method != method)
                                        return await next();

                                    var service = serviceKvp.Value;
                                    var resource = connections[resourceType](service);
                                    return await resource.GetAdaptersAsync(
                                            actorId, serviceKvp.Key.integrationId,
                                        (adapters) =>
                                            onFound(
                                                adapters
                                                    .Select(
                                                        adapter =>
                                                        {
                                                            adapter.integrationId = integrationId;
                                                            return adapter;
                                                        })
                                                    .ToArray()),
                                        onFailure);
                                },
                                onNotFound.AsAsyncFunc());
                        });
                },
                onNotFound.AsAsyncFunc());
        }

        public async Task<TResult> FindByIntgrationAndResourceTypeAsync<TResult>(Guid integrationId, string resourceType,
                Guid actorPerformingAs, System.Security.Claims.Claim[] claims,
            Func<EastFive.Azure.Synchronization.Connection[], TResult> onFound,
            Func<TResult> onNotFound,
            Func<TResult> onUnsupportedResourceType,
            Func<TResult> onUnauthorizedIntegration,
            Func<TResult> onUnauthorized,
            Func<string, TResult> onFailure)
        {
            if (resourceType.IsNullOrWhiteSpace() || (!connections.ContainsKey(resourceType)))
                return onUnsupportedResourceType();

            return await await context.Integrations.GetByIdAsync(integrationId,
                async (actorIdMaybe, method) =>
                {
                    if (!actorIdMaybe.HasValue)
                        return onUnauthorizedIntegration();

                    var actorId = actorIdMaybe.Value;
                    // Get all external items
                    return await GetServicesAsync(actorId,
                        (serviceKvps) =>
                        {
                            return serviceKvps.First(
                                async (serviceKvp, next) =>
                                {
                                    if (serviceKvp.Key.method != method)
                                        return await next();

                                    var service = serviceKvp.Value;
                                    var resource = connections[resourceType](service);
                                    return await resource.SynchronizeAsync(
                                            actorId, actorId, serviceKvp.Key.integrationId,
                                        (synchronizations) => onFound(synchronizations.ToArray()),
                                        onFailure);
                                },
                                onNotFound.AsAsyncFunc());
                        });
                },
                onNotFound.AsAsyncFunc());
        }
        
        internal async Task<TResult> DeleteAsync<TResult>(
                string resourceKey, string resourceType, 
                Guid integrationId,
            Func<TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            var serviceKvps = await context.Integrations.GetActivityAsync<IIntegrate>(integrationId);
            bool[] results = await serviceKvps
                .Select(
                    async (serviceKvp) =>
                    {
                        var service = serviceKvp.Value;
                        var integrationName = serviceKvp.Key.method;
                        var connection = this.connections[resourceType](service);
                        return await connection.DeleteAsync(resourceType, resourceKey,
                            () => true,
                            (why) => false);
                    })
                .WhenAllAsync();
            return onSuccess();
        }
    }
}

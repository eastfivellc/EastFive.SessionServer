using System;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;

using EastFive;
using BlackBarLabs.Extensions;
using BlackBarLabs.Api;
using BlackBarLabs.Linq.Async;
using System.Security.Claims;
using System.Security.Cryptography;
using BlackBarLabs;
using System.Net;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Security.SessionServer;
using EastFive.Security;
using EastFive.Linq.Async;
using EastFive.Api.Azure.Credentials.Attributes;

namespace EastFive.Azure
{
    public struct Integration
    {
        public string method;
        public Guid integrationId;
        public Guid authorizationId;
        public IDictionary<string, string> parameters;
    }

    public class Integrations
    {
        private Context context;
        private Security.SessionServer.Persistence.DataContext dataContext;

        internal Integrations(Context context, Security.SessionServer.Persistence.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }
        
        public async Task<TResult> CreateLinkAsync<TResult>(Guid integrationId, 
                Uri callbackLocation,
                string method, Uri redirectUrl,
                Guid authenticationId, Guid actorId, System.Security.Claims.Claim[] claims,
                Func<Type, Uri> typeToUrl,
            Func<Session, TResult> onSuccess,
            Func<TResult> onAlreadyExists,
            Func<string, TResult> onUnauthorized,
            Func<TResult> onCredentialSystemNotAvailable,
            Func<string, TResult> onCredentialSystemNotInitialized,
            Func<string, TResult> onFailure)
        {
            if (!await Library.configurationManager.CanAdministerCredentialAsync(authenticationId, actorId, claims))
                return onUnauthorized($"Provided token does not permit access to link {authenticationId} to a login");
            return await Context.GetLoginProvider<Task<TResult>>(method,
                async (provider) =>
                {
                    var sessionId = SecureGuid.Generate();
                    return await BlackBarLabs.Security.Tokens.JwtTools.CreateToken<Task<TResult>>(sessionId, callbackLocation, TimeSpan.FromMinutes(30),
                        async (token) => await await this.dataContext.AuthenticationRequests.CreateAsync<Task<TResult>>(integrationId,
                                method, AuthenticationActions.access, authenticationId, token, redirectUrl, redirectUrl,
                            () => dataContext.Integrations.CreateUnauthenticatedAsync(integrationId, authenticationId, method,
                                () => onSuccess(
                                    new Session()
                                    {
                                        id = integrationId,
                                        //method = method,
                                        name = method.ToString(),
                                        action = AuthenticationActions.access,
                                        loginUrl = provider.GetLoginUrl(integrationId, callbackLocation, typeToUrl),
                                        logoutUrl = provider.GetLogoutUrl(integrationId, callbackLocation, typeToUrl),
                                        redirectUrl = redirectUrl,
                                        authorizationId = authenticationId,
                                        token = token,
                                    }),
                                onAlreadyExists),
                        onAlreadyExists.AsAsyncFunc()),
                        why => onFailure(why).ToTask(),
                        (param, why) => onFailure($"Invalid configuration for {param}:{why}").ToTask());
                },
                onCredentialSystemNotAvailable.AsAsyncFunc(),
                onCredentialSystemNotInitialized.AsAsyncFunc());
        }

        public async Task<TResult> CreateOrUpdateParamsByActorAsync<TResult>(Guid actorId, string method,
            Func<
                Guid,
                IDictionary<string, string>,
                Func<IDictionary<string, string>, Task>,
                Task<TResult>> onFound,
            Func<
                Func<IDictionary<string, string>, Task<Guid>>, Task<TResult>> onNotFound)
        {
            return await this.dataContext.Integrations.FindUpdatableAsync(actorId, method,
                onFound,
                (createAsync) =>
                {
                    return onNotFound(
                        async (parameters) =>
                        {
                            var integrationId = Guid.NewGuid();
                            return await await this.dataContext.AuthenticationRequests.CreateAsync(integrationId, method, AuthenticationActions.link, actorId,
                                string.Empty, default(Uri), default(Uri),
                                async () =>
                                {
                                    await createAsync(integrationId, parameters);
                                    return integrationId;
                                },
                                "Guid not unique".AsFunctionException<Task<Guid>>());
                        });
                });
        }

        [Obsolete("Use method string instead.")]
        public async Task<TResult> CreateOrUpdateAuthenticatedIntegrationAsync<TResult>(Guid actorId, Api.Azure.Credentials.CredentialValidationMethodTypes method,
           Func<
               Guid?,
               IDictionary<string, string>,
               Func<IDictionary<string, string>, Task<Guid>>,
               Task<TResult>> onCreatedOrFound)
        {
            var methodName = Enum.GetName(typeof(Api.Azure.Credentials.CredentialValidationMethodTypes), method);
            return await this.dataContext.Integrations.CreateOrUpdateAsync(actorId, methodName,
                (integrationIdMaybe, paramsCurrent, updateAsync) =>
                {
                    return onCreatedOrFound(integrationIdMaybe, paramsCurrent, updateAsync);
                });
        }

        public async Task<TResult> CreateOrUpdateAuthenticatedIntegrationAsync<TResult>(Guid actorId, string method,
           Func<
               Guid?,
               IDictionary<string, string>,
               Func<IDictionary<string, string>, Task<Guid>>,
               Task<TResult>> onCreatedOrFound)
        {
            return await this.dataContext.Integrations.CreateOrUpdateAsync(actorId, method,
                (integrationIdMaybe, paramsCurrent, updateAsync) =>
                {
                    return onCreatedOrFound(integrationIdMaybe, paramsCurrent, updateAsync);
                });
        }

        internal async Task<TResult> GetAsync<TResult>(Guid authenticationRequestId, Func<Type, Uri> callbackUrlFunc,
            Func<Session, TResult> onSuccess,
            Func<TResult> onNotFound,
            Func<string, TResult> onFailure)
        {
            return await await this.dataContext.AuthenticationRequests.FindByIdAsync(authenticationRequestId,
                async (authenticationRequestStorage) =>
                {
                    return await Context.GetLoginProvider(authenticationRequestStorage.method,
                        async (provider) =>
                        {
                            var extraParams = authenticationRequestStorage.extraParams;
                            return await await provider.UserParametersAsync(authenticationRequestStorage.authorizationId.Value, null, extraParams,
                                async (labels, types, descriptions) =>
                                {
                                    var callbackUrl = callbackUrlFunc(provider.CallbackController);
                                    var loginUrl = provider.GetLoginUrl(authenticationRequestId, callbackUrl, callbackUrlFunc);
                                    var authenticationRequest = await Convert(authenticationRequestStorage, loginUrl, extraParams, labels, types, descriptions);
                                    return onSuccess(authenticationRequest);
                                });
                        },
                        () => onFailure("The credential provider for this request is no longer enabled in this system").ToTask(),
                        (why) => onFailure(why).ToTask());
                },
                ()=> onNotFound().ToTask());
        }

        public Task<TResult> GetByIdAsync<TResult>(Guid authenticationRequestId,
            Func<Guid?, string, TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return this.dataContext.AuthenticationRequests.FindByIdAsync(authenticationRequestId,
                (authenticationRequestStorage) => onSuccess(authenticationRequestStorage.authorizationId, authenticationRequestStorage.method),
                () => onNotFound());
        }

        public Task<TResult> GetAuthenticatedByIdAsync<TResult>(Guid authenticationRequestId,
            Func<Integration, TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return this.dataContext.AuthenticationRequests.FindByIdAsync(authenticationRequestId,
                (authenticationRequestStorage) =>
                {
                    if (!authenticationRequestStorage.authorizationId.HasValue)
                        return onNotFound();
                    return onSuccess(
                        new Integration
                        {
                            authorizationId = authenticationRequestStorage.authorizationId.Value,
                            integrationId = authenticationRequestId,
                            method = authenticationRequestStorage.method,
                            parameters = authenticationRequestStorage.extraParams,
                        });
                },
                () => onNotFound());
        }

        internal async Task<TResult> GetByActorAsync<TResult>(Guid actorId, Func<Type, Uri> callbackUrlFunc,
                Guid actingAs, System.Security.Claims.Claim [] claims,
            Func<Session[], TResult> onSuccess,
            Func<TResult> onNotFound,
            Func<TResult> onUnathorized,
            Func<string, TResult> onFailure)
        {
            if (!await Library.configurationManager.CanAdministerCredentialAsync(actorId,
                actingAs, claims))
                return onUnathorized();

            var integrations = await ServiceConfiguration.loginProviders
                .Select(
                    async ap => await await this.dataContext.Integrations.FindAsync<Task<Session?>>(actorId, ap.Key,
                        async (authenticationRequestId) =>
                        {
                            var provider = ap.Value;
                            var method = ap.Key;
                            return await await this.dataContext.AuthenticationRequests.FindByIdAsync(authenticationRequestId,
                                async (authRequest) =>
                                {
                                    return await await provider.UserParametersAsync(actorId, null, null,
                                        async (labels, types, descriptions) =>
                                        {
                                            var callbackUrl = callbackUrlFunc(provider.CallbackController);
                                            var loginUrl = provider.GetLoginUrl(Guid.Empty, callbackUrl, callbackUrlFunc);
                                            var authenticationRequest = await Convert(authenticationRequestId, ap.Key, AuthenticationActions.access,
                                                default(string), authenticationRequestId, loginUrl, default(Uri), authRequest.extraParams, labels, types, descriptions);
                                            return authenticationRequest;
                                        });
                                },
                                async () =>
                                {
                                    #region SHIM
                                    var integrationId = authenticationRequestId;
                                    return await await this.dataContext.AuthenticationRequests.CreateAsync(integrationId, method, AuthenticationActions.link, default(Uri), default(Uri),
                                        async () =>
                                        {
                                            return await await provider.UserParametersAsync(actorId, null, null,
                                                async (labels, types, descriptions) =>
                                                {
                                                    var callbackUrl = callbackUrlFunc(provider.CallbackController);
                                                    var loginUrl = provider.GetLoginUrl(Guid.Empty, callbackUrl, callbackUrlFunc);
                                                    var authenticationRequest = await Convert(authenticationRequestId, ap.Key, AuthenticationActions.access,
                                                        default(string), authenticationRequestId, loginUrl, default(Uri), default(IDictionary<string, string>), labels, types, descriptions);
                                                    return authenticationRequest;
                                                });
                                        },
                                        "Guid not unique".AsFunctionException<Task<Session>>());
                                    #endregion
                                });
                            
                        },
                        () => default(Session?).ToTask()))
                .WhenAllAsync()
                .SelectWhereHasValueAsync()
                .ToArrayAsync();
            return onSuccess(integrations);
        }

        internal async Task<TResult> GetAllAsync<TResult>(
                Func<Type, Uri> callbackUrlFunc,
                Guid actingAs, System.Security.Claims.Claim[] claims,
            Func<Session[], TResult> onSuccess,
            Func<TResult> onNotFound,
            Func<TResult> onUnathorized,
            Func<string, TResult> onFailure)
        {
            var accesses = await this.dataContext.Integrations.FindAllAsync();
            var integrations = await accesses
                .FlatMap(
                    async (accessKvp, next, skip) =>
                    {
                        var access = accessKvp.Key;
                        if (!await Library.configurationManager.CanAdministerCredentialAsync(access.authorizationId, actingAs, claims))
                            return await skip();
                        var session = new Session
                        {
                            authorizationId = access.authorizationId,
                            extraParams = access.parameters, // TODO: Only if super admin!!
                            method = access.method,
                            name = access.method,
                        };
                        if (!accessKvp.Value.HasValue)
                            return await next(session);

                        var authRequest = accessKvp.Value.Value;
                        session.id = authRequest.id;
                        session.action = authRequest.action;
                        //session.extraParams = authRequest.extraParams;
                        session.token = authRequest.token;
                        return await next(session);
                    },
                    (IEnumerable<Session> sessions) => sessions.ToArray().ToTask());
            return onSuccess(integrations);
        }

        public async Task<KeyValuePair<Integration, T>[]> GetActivitiesAsync<T>(Guid actorId)
        {
            if (!ServiceConfiguration.integrationActivites.ContainsKey(typeof(T)))
                return new KeyValuePair<Integration, T>[] { };
            var activitiesOfTypeT = ServiceConfiguration.integrationActivites[typeof(T)];

            var integrations = await dataContext.Integrations.FindAsync(actorId);
            var integrationsMatchingActivities = integrations
                .Where(integration => activitiesOfTypeT.ContainsKey(integration.method))
                .ToArray();
            return await integrationsMatchingActivities
                .Select(
                    integration => activitiesOfTypeT[integration.method]
                        .FlatMap(
                            (invocation, next, skip) =>
                            {
                                return (Task<KeyValuePair<Integration, T>[]>)invocation(integration,
                                    async (obj) => await next(integration.PairWithValue((T)obj)),
                                    async (why) => await skip());
                            },
                            (IEnumerable<KeyValuePair<Integration, T>> integrationsKvp) =>
                            {
                                var integrationsKvpArray = integrationsKvp.ToArray();
                                return integrationsKvpArray.ToTask();
                            }))
                .WhenAllAsync()
                .SelectManyAsync()
                .ToArrayAsync();
        }

        public async Task<KeyValuePair<Integration, T[]>> GetActivityAsync<T>(Guid integrationId)
        {
            var activities = await GetActivityAsync(integrationId, typeof(T));
            return activities.Key.PairWithValue(activities.Value
                .Select(activity => (T)activity)
                .ToArray());
        }

        public async Task<KeyValuePair<Integration, object[]>> GetActivityAsync(Guid integrationId, Type typeofT)
        {
            if (!ServiceConfiguration.integrationActivites.ContainsKey(typeofT))
                return new KeyValuePair<Integration, object[]> { };
            var activitiesOfTypeT = ServiceConfiguration.integrationActivites[typeofT];

            return await await dataContext.Integrations.FindAuthorizedAsync(integrationId,
                async integration =>
                {
                    if (!activitiesOfTypeT.ContainsKey(integration.method))
                        return new KeyValuePair<Integration, object[]> { };
                    return await activitiesOfTypeT[integration.method]
                        .FlatMap(
                            (invocation, next, skip) =>
                            {
                                return (Task<KeyValuePair<Integration, object[]>>)invocation(integration,
                                    async (obj) => await next(obj),
                                    async (why) => await skip());
                            },
                            (IEnumerable<object> activities) =>
                            {
                                var activitiesArray = activities.ToArray();
                                return integration.PairWithValue(activitiesArray).ToTask();
                            });
                },
                () => (new KeyValuePair<Integration, object[]> { }).ToTask());
        }

        public async Task<TResult> GetAsync<TIntegration, TResult>(Guid actorId,
            Func<TIntegration, TResult> onEnabled,
            Func<TResult> onDisabled,
            Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
            //return ServiceConfiguration.integrations.
        }

        public async Task<TResult> GetParamsByActorAsync<TResult>(Guid actorId, string method,
            Func<IDictionary<string, string>, TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return await await dataContext.Integrations.FindAsync(actorId, method,
                async (authenticationRequestId) =>
                {
                    return await this.dataContext.AuthenticationRequests.FindByIdAsync(authenticationRequestId,
                        (authRequest) =>
                        {
                            return onSuccess(authRequest.extraParams);
                        },
                        onNotFound);
                },
                ()=> onNotFound().ToTask());
        }

        public Task<TResult> UpdateAsync<TResult>(Guid authenticationRequestId,
              string token, IDictionary<string, string> updatedUserParameters,
          Func<Uri, TResult> onUpdated,
          Func<TResult> onAutheticationRequestNotFound,
          Func<TResult> onUnauthenticatedAuthenticationRequest)
        {
            return dataContext.AuthenticationRequests.UpdateAsync(authenticationRequestId,
                async (authRequestStorage, saveAsync) =>
                {
                    if (!authRequestStorage.authorizationId.HasValue)
                        return onUnauthenticatedAuthenticationRequest();

                    await saveAsync(authRequestStorage.authorizationId.Value, token, updatedUserParameters);
                    return onUpdated(authRequestStorage.redirect);
                },
                onAutheticationRequestNotFound);
        }

        public Task<TResult> UpdateAsync<TResult>(Guid authenticationRequestId, Guid actingAsUser,
                System.Security.Claims.Claim[] claims, Api.Azure.Application application,
              IDictionary<string, string> updatedUserParameters,
          Func<TResult> onUpdated,
          Func<Guid, Guid, string, string, AuthenticationActions, IDictionary<string, string>, Uri, TResult> onLogin,
          Func<Uri, string, TResult> onLogout,
          Func<string, TResult> onInvalidToken,
          Func<TResult> onLookupCredentialNotFound,
          Func<string, TResult> onSystemOffline,
          Func<string, TResult> onNotConfigured,
          Func<string, TResult> onFailure)
        {
            return dataContext.AuthenticationRequests.UpdateAsync(authenticationRequestId,
                (authRequestStorage, saveAsync) =>
                {
                    if (!authRequestStorage.authorizationId.HasValue)
                    {
                        var method = authRequestStorage.method;
                        return context.Sessions.UpdateWithAuthenticationAsync(authenticationRequestId,
                                application, method, updatedUserParameters,
                            onLogin,
                            onLogout,
                            onInvalidToken,
                            onLookupCredentialNotFound,
                            onSystemOffline,
                            onNotConfigured,
                            onFailure);
                    }

                    return UpdateUserParameters(authRequestStorage.authorizationId.Value, authRequestStorage.method, actingAsUser, 
                        claims, authRequestStorage.token, authRequestStorage.extraParams, updatedUserParameters, saveAsync, onUpdated, onFailure );
                },
                onLookupCredentialNotFound);
        }

        private static Task<TResult> UpdateUserParameters<TResult>(Guid authorizationId, string method, Guid actingAsUser, System.Security.Claims.Claim[] claims,
            string token,
            IDictionary<string, string> extraParams,
            IDictionary<string, string> updatedUserParameters,
            Func<Guid, string, IDictionary<string, string>, Task> saveAsync,
            Func<TResult> onUpdated,
            Func<string, TResult> onFailure)
        {
            return Context.GetLoginProvider(method,
                async (provider) =>
                {
                    var userHash = await provider.UserParametersAsync(actingAsUser, claims, extraParams,
                        (labels, types, descriptions) =>
                        {
                            return labels.SelectKeys().Concat(types.SelectKeys()).Concat(descriptions.SelectKeys())
                                .Distinct()
                                .AsHashSet(StringComparer.InvariantCultureIgnoreCase);
                        });

                    var mergedExtraParams = extraParams
                        .Aggregate(
                            updatedUserParameters
                                .Where(param => userHash.Contains(param.Key))
                                .ToDictionary(),
                            (userParametersBeingUpdated, extraParamFromStorage) =>
                            {
                                if (userParametersBeingUpdated.ContainsKey(extraParamFromStorage.Key))
                                    return userParametersBeingUpdated;

                                return userParametersBeingUpdated.Append(extraParamFromStorage).ToDictionary();
                            });
                    await saveAsync(authorizationId, token, mergedExtraParams);
                    return onUpdated();
                },
                () => onFailure("Integration is no longer available for the system given").ToTask(),
                onFailure.AsAsyncFunc());
        }


        //internal async Task<TResult> SetAsAuthenticatedAsync<TResult>(Persistence.AuthenticationRequest authenticationRequest,
        //        Guid sessionId,
        //        CredentialValidationMethodTypes method, IDictionary<string, string> extraParams,
        //    Func<Guid, string, string, Uri, TResult> onSuccess,
        //    Func<TResult> onAlreadyAuthenticated,
        //    Func<string, TResult> onNotConfigured,
        //    Func<string, TResult> onFailure)
        //{
        //    if (!authenticationRequest.authorizationId.HasValue)
        //        return onFailure("The credential is corrupt");

        //    var authenticationId = authenticationRequest.authorizationId.Value;
        //    return await await dataContext.Accesses.CreateAsync(authenticationRequest.id, authenticationId, 
        //            method,
        //        () => context.Sessions.GenerateSessionWithClaimsAsync(sessionId, authenticationId,
        //            (token, refreshToken) =>
        //            {
        //                return onSuccess(authenticationId, token, refreshToken,
        //                        authenticationRequest.redirect);
        //            },
        //            onNotConfigured),
        //        () => onAlreadyAuthenticated().ToTask());
        //}

        public async Task<TResult> DeleteByIdAsync<TResult>(Guid accessId,
                Guid performingActorId, System.Security.Claims.Claim [] claims, HttpRequestMessage request,
            Func<HttpResponseMessage, TResult> onSuccess, 
            Func<TResult> onNotFound,
            Func<TResult> onUnathorized)
        {
            return await await this.dataContext.AuthenticationRequests.DeleteByIdAsync(accessId,
                async (integration, deleteAsync) =>
                {
                    var integrationDeleted = await Convert(integration, default(Uri), default(Dictionary<string, string>),
                                   default(Dictionary<string, string>), default(Dictionary<string, Type>), default(Dictionary<string, string>));
                    if (!integration.authorizationId.HasValue)
                    {
                        
                        return await Library.configurationManager.RemoveIntegrationAsync(integrationDeleted, request,
                            async (response) =>
                            {
                                await deleteAsync();
                                return onSuccess(response);
                            },
                            () => onSuccess(request.CreateResponse(HttpStatusCode.InternalServerError).AddReason("failure")).ToTask());
                    }
                    
                    return await dataContext.Integrations.DeleteAsync(integration.authorizationId.Value, integration.method,
                        async (parames) =>
                        {
                            return await await Library.configurationManager.RemoveIntegrationAsync(integrationDeleted, request,
                                async (response) =>
                                {
                                    await deleteAsync();
                                    return onSuccess(response);
                                },
                                () => onSuccess(request.CreateResponse(HttpStatusCode.InternalServerError).AddReason("failure")).ToTask());
                        },
                        onNotFound.AsAsyncFunc());
                },
                async () =>
                {
                    var x = await context.GetLoginProviders<Task<bool[]>>(
                        async (accessProviders) =>
                        {
                            return await accessProviders
                                .Select(
                                    accessProvider =>
                                    {
                                        return dataContext.Integrations.DeleteAsync(performingActorId,
                                                accessProvider.GetType().GetCustomAttribute<IntegrationNameAttribute>().Name,
                                            (parames) => true,
                                            () => false);
                                    })
                                .WhenAllAsync();
                        },
                        (why) => (new bool[] { }).ToTask());
                    return onSuccess(request.CreateResponse(HttpStatusCode.NoContent).AddReason("Access ID not found"));
                });
        }

        private static Task<Session> Convert(
            Security.SessionServer.Persistence.AuthenticationRequest authenticationRequest,
            Uri loginUrl,
            IDictionary<string, string> extraParams, 
            IDictionary<string, string> labels, 
            IDictionary<string, Type> types, 
            IDictionary<string, string> descriptions)
        {
            return Convert(authenticationRequest.id, authenticationRequest.method, authenticationRequest.action, authenticationRequest.token, 
                authenticationRequest.authorizationId.Value, loginUrl, authenticationRequest.redirect, extraParams, labels, types, descriptions);
        }

        private async static Task<Session> Convert(
            Guid authenticationRequestStorageId,
            string methodName,
            AuthenticationActions action,
            string token,
            Guid authorizationId,
            Uri loginUrl,
            Uri redirect,
            IDictionary<string, string> extraParams,
            IDictionary<string, string> labels,
            IDictionary<string, Type> types,
            IDictionary<string, string> descriptions)
        {
            var keys = labels.SelectKeys().Concat(types.SelectKeys()).Concat(descriptions.SelectKeys());
            var userParams = keys
                .Distinct()
                .Select(
                    key =>
                    {
                        var val = default(string);
                        if (null != extraParams)
                            val = extraParams.ContainsKey(key) ? extraParams[key] : default(string);

                        return (new CustomParameter
                        {
                            Value = val,
                            Type = types.ContainsKey(key) ? types[key] : default(Type),
                            Label = labels.ContainsKey(key) ? labels[key] : default(string),
                            Description = descriptions.ContainsKey(key) ? descriptions[key] : default(string),
                        }).PairWithKey(key);
                    })
                .ToDictionary();

            var resourceTypes = await ServiceConfiguration.IntegrationResourceTypesAsync(authenticationRequestStorageId,
                (resourceTypesInner) => resourceTypesInner,
                () => new string[] { });

            return new Session
            {
                id = authenticationRequestStorageId,
                method = methodName,
                name = methodName,
                action = action,
                token = token,
                authorizationId = authorizationId,
                redirectUrl = redirect,
                loginUrl = loginUrl,
                userParams = userParams,
                resourceTypes = resourceTypes
                    .Select(resourceType => resourceType.PairWithValue(resourceType))
                    .ToDictionary(),
            };
        }
    }
}

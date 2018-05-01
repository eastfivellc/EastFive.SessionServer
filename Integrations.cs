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

namespace EastFive.Security.SessionServer
{
    public class Integrations
    {
        private Context context;
        private Persistence.DataContext dataContext;

        internal Integrations(Context context, Persistence.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }
        
        public async Task<TResult> CreateLinkAsync<TResult>(Guid authenticationRequestId, 
                Uri callbackLocation,
                CredentialValidationMethodTypes method, Uri redirectUrl,
                Guid authenticationId, Guid actorId, System.Security.Claims.Claim[] claims,
            Func<Session, TResult> onSuccess,
            Func<TResult> onAlreadyExists,
            Func<string, TResult> onUnauthorized,
            Func<TResult> onCredentialSystemNotAvailable,
            Func<string, TResult> onCredentialSystemNotInitialized,
            Func<string, TResult> onFailure)
        {
            if (!await Library.configurationManager.CanAdministerCredentialAsync(authenticationId, actorId, claims))
                return onUnauthorized($"Provided token does not permit access to link {authenticationId} to a login");
            return await context.GetLoginProvider(method,
                async (provider) =>
                {
                    var sessionId = SecureGuid.Generate();
                    return await BlackBarLabs.Security.Tokens.JwtTools.CreateToken(sessionId, callbackLocation, TimeSpan.FromMinutes(30),
                            (token) => this.dataContext.AuthenticationRequests.CreateAsync(authenticationRequestId,
                                    method, AuthenticationActions.access, authenticationId, token, redirectUrl, redirectUrl,
                                () => onSuccess(
                                    new Session()
                                    {
                                        id = authenticationRequestId,
                                        method = method,
                                        action = AuthenticationActions.access,
                                        loginUrl = provider.GetLoginUrl(authenticationRequestId, callbackLocation),
                                        logoutUrl = provider.GetLogoutUrl(authenticationRequestId, callbackLocation),
                                        redirectUrl = redirectUrl,
                                        authorizationId = authenticationId,
                                        token = token,
                                    }),
                                onAlreadyExists),
                            why => onFailure(why).ToTask(),
                            (param, why) => onFailure($"Invalid configuration for {param}:{why}").ToTask());
                },
                onCredentialSystemNotAvailable.AsAsyncFunc(),
                onCredentialSystemNotInitialized.AsAsyncFunc());
        }

        internal async Task<TResult> GetAsync<TResult>(Guid authenticationRequestId, Func<Type, Uri> callbackUrlFunc,
            Func<Session, TResult> onSuccess,
            Func<TResult> onNotFound,
            Func<string, TResult> onFailure)
        {
            return await await this.dataContext.AuthenticationRequests.FindByIdAsync(authenticationRequestId,
                async (authenticationRequestStorage) =>
                {
                    return await context.GetLoginProvider(authenticationRequestStorage.method,
                        async (provider) =>
                        {
                            var extraParams = authenticationRequestStorage.extraParams;
                            return await provider.UserParametersAsync(authenticationRequestStorage.authorizationId.Value, null, extraParams,
                                (labels, types, descriptions) =>
                                {
                                    var callbackUrl = callbackUrlFunc(provider.CallbackController);
                                    var loginUrl = provider.GetLoginUrl(authenticationRequestId, callbackUrl);
                                    var authenticationRequest = Convert(authenticationRequestStorage, loginUrl, extraParams, labels, types, descriptions);
                                    return onSuccess(authenticationRequest);
                                });
                        },
                        () => onFailure("The credential provider for this request is no longer enabled in this system").ToTask(),
                        (why) => onFailure(why).ToTask());
                },
                ()=> onNotFound().ToTask());
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
                    async ap => await await this.dataContext.Accesses.FindAsync(actorId, ap.Key,
                        async (authenticationRequestId, extraParams) =>
                        {
                            return await context.GetLoginProvider<Task<Session?>>(ap.Key,
                                async (provider) =>
                                {
                                    return await await this.dataContext.AuthenticationRequests.FindByIdAsync(authenticationRequestId,
                                        async (authRequest) =>
                                        {
                                            return await provider.UserParametersAsync(actorId, null, extraParams,
                                                (labels, types, descriptions) =>
                                                {
                                                    var callbackUrl = callbackUrlFunc(provider.CallbackController);
                                                    var loginUrl = provider.GetLoginUrl(Guid.Empty, callbackUrl);
                                                    var authenticationRequest = Convert(authenticationRequestId, ap.Key, AuthenticationActions.access,
                                                        default(string), authenticationRequestId, loginUrl, default(Uri), extraParams, labels, types, descriptions);
                                                    return authenticationRequest;
                                                });
                                        },
                                        async () =>
                                        {
                                            #region SHIM
                                            var integrationId = authenticationRequestId;
                                            return await await this.dataContext.AuthenticationRequests.CreateAsync(integrationId, ap.Key, AuthenticationActions.link, actorId,
                                                string.Empty, default(Uri), default(Uri),
                                                async () =>
                                                {
                                                    return await provider.UserParametersAsync(actorId, null, extraParams,
                                                        (labels, types, descriptions) =>
                                                        {
                                                            var callbackUrl = callbackUrlFunc(provider.CallbackController);
                                                            var loginUrl = provider.GetLoginUrl(Guid.Empty, callbackUrl);
                                                            var authenticationRequest = Convert(authenticationRequestId, ap.Key, AuthenticationActions.access,
                                                                default(string), authenticationRequestId, loginUrl, default(Uri), extraParams, labels, types, descriptions);
                                                            return authenticationRequest;
                                                        });
                                                },
                                                "Guid not unique".AsFunctionException<Task<Session>>());
                                            #endregion
                                        });

                                },
                                () => default(Session?).ToTask(),
                                (why) => default(Session?).ToTask());
                        },
                        () => default(Session?).ToTask()))
                .WhenAllAsync()
                .SelectWhereHasValueAsync()
                .ToArrayAsync();
            return onSuccess(integrations);
        }

        public async Task<TResult> GetAsync<TIntegration, TResult>(Guid actorId,
            Func<TIntegration, TResult> onEnabled,
            Func<TResult> onDisabled,
            Func<string, TResult> onFailure)
        {
            return ServiceConfiguration.integrations.
        }

        public async Task<TResult> GetParamsByActorAsync<TResult>(Guid actorId, CredentialValidationMethodTypes method,
            Func<Guid, IDictionary<string, string>, Func<IDictionary<string, string>, Task>, Task<TResult>> onSuccess,
            Func<TResult> onNotFound)
        {
            return await this.dataContext.Accesses.FindAsync(actorId, method,
                onSuccess,
                (createAsync) => onNotFound().ToTask());
        }

        public async Task<TResult> CreateOrUpdateParamsByActorAsync<TResult>(Guid actorId, CredentialValidationMethodTypes method,
            Func<
                Guid,
                IDictionary<string, string>, 
                Func<IDictionary<string, string>, Task>, 
                Task<TResult>> onFound,
            Func<
                Func<IDictionary<string, string>, Task<Guid>>, Task<TResult>> onNotFound)
        {
            return await this.dataContext.Accesses.FindUpdatableAsync(actorId, method,
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

        internal async Task<TResult> SetAsAuthenticatedAsync<TResult>(Persistence.AuthenticationRequest authenticationRequest,
                Guid sessionId,
                CredentialValidationMethodTypes method, IDictionary<string, string> extraParams,
            Func<Guid, string, string, Uri, TResult> onSuccess,
            Func<TResult> onAlreadyAuthenticated,
            Func<string, TResult> onNotConfigured,
            Func<string, TResult> onFailure)
        {
            if (!authenticationRequest.authorizationId.HasValue)
                return onFailure("The credential is corrupt");

            var authenticationId = authenticationRequest.authorizationId.Value;
            return await await dataContext.Accesses.CreateAsync(authenticationRequest.id, authenticationId, 
                    method,
                () => context.Sessions.GenerateSessionWithClaimsAsync(sessionId, authenticationId,
                    (token, refreshToken) =>
                    {
                        return onSuccess(authenticationId, token, refreshToken,
                                authenticationRequest.redirect);
                    },
                    onNotConfigured),
                () => onAlreadyAuthenticated().ToTask());
        }

        public async Task<TResult> DeleteByIdAsync<TResult>(Guid accessId,
                Guid performingActorId, System.Security.Claims.Claim [] claims, HttpRequestMessage request,
            Func<HttpResponseMessage, TResult> onSuccess, 
            Func<TResult> onNotFound,
            Func<TResult> onUnathorized)
        {
            return await await this.dataContext.AuthenticationRequests.DeleteByIdAsync(accessId,
                async (integration, deleteAsync) =>
                {
                    if(!integration.authorizationId.HasValue)
                        return await Library.configurationManager.RemoveIntegrationAsync(Convert(integration, default(Uri), default(Dictionary<string, string>),
                                default(Dictionary<string, string>), default(Dictionary<string, Type>), default(Dictionary<string, string>)), request,
                            async (response) =>
                            {
                                await deleteAsync();
                                return onSuccess(response);
                            },
                            () => onSuccess(request.CreateResponse(HttpStatusCode.InternalServerError).AddReason("failure")).ToTask());

                    return await dataContext.Accesses.DeleteAsync(integration.authorizationId.Value, integration.method,
                        async (method, parames) =>
                        {
                            return await await Library.configurationManager.RemoveIntegrationAsync(Convert(integration, default(Uri), default(Dictionary<string, string>),
                                    default(Dictionary<string, string>), default(Dictionary<string, Type>), default(Dictionary<string, string>)), request,
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
                    var x = await context.GetLoginProviders(
                        async (accessProviders) =>
                        {
                            return await accessProviders
                                .Select(
                                    accessProvider =>
                                    {
                                        return dataContext.Accesses.DeleteAsync(performingActorId, accessProvider.Method,
                                            (method, parames) => true,
                                            () => false);
                                    })
                                .WhenAllAsync();
                        },
                        (why) => (new bool[] { }).ToTask());
                    return onSuccess(request.CreateResponse(HttpStatusCode.NoContent).AddReason("Access ID not found"));
                });
        }

        private static Session Convert(
            Persistence.AuthenticationRequest authenticationRequest,
            Uri loginUrl,
            IDictionary<string, string> extraParams, 
            IDictionary<string, string> labels, 
            IDictionary<string, Type> types, 
            IDictionary<string, string> descriptions)
        {
            return Convert(authenticationRequest.id, authenticationRequest.method, authenticationRequest.action, authenticationRequest.token, 
                authenticationRequest.authorizationId.Value, loginUrl, authenticationRequest.redirect, extraParams, labels, types, descriptions);
        }

        private static Session Convert(
            Guid authenticationRequestStorageId,
            CredentialValidationMethodTypes method,
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
                        return (new CustomParameter
                        {
                            Value = extraParams.ContainsKey(key) ? extraParams[key] : default(string),
                            Type = types.ContainsKey(key) ? types[key] : default(Type),
                            Label = labels.ContainsKey(key) ? labels[key] : default(string),
                            Description = descriptions.ContainsKey(key) ? descriptions[key] : default(string),
                        }).PairWithKey(key);
                    })
                .ToDictionary();

            return new Session
            {
                id = authenticationRequestStorageId,
                method = method,
                action = action,
                token = token,
                authorizationId = authorizationId,
                redirectUrl = redirect,
                loginUrl = loginUrl,
                userParams = userParams
            };
        }
    }
}

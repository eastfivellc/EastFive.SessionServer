using System;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;

using BlackBarLabs.Extensions;
using BlackBarLabs.Api;
using BlackBarLabs.Linq.Async;
using System.Security.Claims;
using System.Security.Cryptography;
using BlackBarLabs;

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
                    var result = await this.dataContext.AuthenticationRequests.CreateAsync(authenticationRequestId,
                            method, AuthenticationActions.access, authenticationId, redirectUrl, redirectUrl,
                        () => BlackBarLabs.Security.Tokens.JwtTools.CreateToken(sessionId, callbackLocation, TimeSpan.FromMinutes(30),
                            (token) => onSuccess(
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
                            why => onFailure(why),
                            (param, why) => onFailure($"Invalid configuration for {param}:{why}")),
                        onAlreadyExists);
                    return result;
                },
                onCredentialSystemNotAvailable.AsAsyncFunc(),
                onCredentialSystemNotInitialized.AsAsyncFunc());
        }

        internal async Task<TResult> GetAsync<TResult>(Guid authenticationRequestId, Uri callbackUrl,
            Func<Session, TResult> onSuccess,
            Func<TResult> onNotFound,
            Func<string, TResult> onFailure)
        {
            return await this.dataContext.AuthenticationRequests.FindByIdAsync(authenticationRequestId,
                (authenticationRequestStorage) =>
                {
                    return context.GetLoginProvider(authenticationRequestStorage.method,
                        (provider) =>
                        {
                            var authenticationRequest = Convert(authenticationRequestStorage);
                            authenticationRequest.loginUrl = provider.GetLoginUrl(authenticationRequestId, callbackUrl);
                            return onSuccess(authenticationRequest);
                        },
                        () => onFailure("The credential provider for this request is no longer enabled in this system"),
                        (why) => onFailure(why));
                },
                onNotFound);
        }

        internal async Task<TResult> GetByActorAsync<TResult>(Guid actorId, Uri callbackUrl,
                Guid actingAs, System.Security.Claims.Claim [] claims,
            Func<Session[], TResult> onSuccess,
            Func<TResult> onNotFound,
            Func<TResult> onUnathorized,
            Func<string, TResult> onFailure)
        {
            if (!await Library.configurationManager.CanAdministerCredentialAsync(actorId,
                actingAs, claims))
                return onUnathorized();

            var integrations = await ServiceConfiguration.accessProviders
                .Select(
                    ap => this.dataContext.Accesses.FindAsync(actorId, ap.Key,
                        (authenticationRequestId, extraParams) =>
                            context.GetLoginProvider(ap.Key,
                                (provider) =>
                                {
                                    var authenticationRequest = new Session
                                    {
                                        id = authenticationRequestId,
                                        action = AuthenticationActions.access,
                                        authorizationId = actorId,
                                        extraParams = extraParams,
                                        loginUrl = provider.GetLoginUrl(Guid.Empty, callbackUrl),
                                        method = ap.Key,
                                    };
                                    return authenticationRequest;
                                },
                                () => default(Session?),
                                (why) => default(Session?)),
                        () => default(Session?)))
                .WhenAllAsync()
                .SelectWhereHasValueAsync()
                .ToArrayAsync();
            return onSuccess(integrations);
        }

        public async Task<TResult> GetParamsByActorAsync<TResult>(Guid actorId, CredentialValidationMethodTypes method,
            Func<Guid, IDictionary<string, string>, Func<IDictionary<string, string>, Task>, Task<TResult>> onSuccess,
            Func<TResult> onNotFound)
        {
            return await this.dataContext.Accesses.FindUpdatableAsync(actorId, method,
                onSuccess,
                onNotFound);
        }

        internal async Task<TResult> UpdateAsync<TResult>(Persistence.AuthenticationRequest authenticationRequest,
                Guid sessionId, Guid stateId,
                CredentialValidationMethodTypes method, IDictionary<string, string> extraParams,
                Func<Guid, string, IDictionary<string, string>, Task> saveAuthRequest,
            Func<Guid, Guid, string, string, AuthenticationActions, IDictionary<string, string>, Uri, TResult> onSuccess,
            Func<string, TResult> onInvalidToken,
            Func<string, TResult> onNotConfigured,
            Func<string, TResult> onFailure)
        {
            if (!authenticationRequest.authorizationId.HasValue)
                return onFailure("The credential is corrupt");

            var authenticationId = authenticationRequest.authorizationId.Value;
            return await await dataContext.Accesses.CreateAsync(authenticationRequest.id, authenticationId, 
                    method, extraParams,
                async () => await await context.Sessions.CreateSessionAsync(sessionId, authenticationId,
                    async (token, refreshToken) =>
                    {
                        await saveAuthRequest(authenticationId, token, extraParams);
                        return onSuccess(stateId, authenticationId, token, refreshToken,
                                AuthenticationActions.access, extraParams,
                                authenticationRequest.redirect);
                    },
                    onNotConfigured.AsAsyncFunc()),
                () => onInvalidToken("Login is already mapped to an access.").ToTask());
        }

        public async Task<TResult> DeleteByIdAsync<TResult>(Guid accessId,
                Guid performingActorId, System.Security.Claims.Claim [] claims,
            Func<Uri, TResult> onSuccess, 
            Func<TResult> onNotFound,
            Func<TResult> onUnathorized)
        {
            return await await this.dataContext.AuthenticationRequests.DeleteByIdAsync(accessId,
                async (integration, deleteAsync) =>
                {
                    if(!integration.authorizationId.HasValue)
                        return await Library.configurationManager.RemoveIntegrationAsync(Convert(integration),
                            async (uri) =>
                            {
                                await deleteAsync();
                                return onSuccess(uri);
                            },
                            () => onSuccess(default(Uri)).ToTask());

                    return await dataContext.Accesses.DeleteAsync(integration.authorizationId.Value, integration.method,
                        async (method, parames) =>
                        {
                            return await await Library.configurationManager.RemoveIntegrationAsync(Convert(integration),
                                async (uri) =>
                                {
                                    await deleteAsync();
                                    return onSuccess(uri);
                                },
                                () => onSuccess(default(Uri)).ToTask());
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
                    return onSuccess(default(Uri));
                });
        }

        private static Session Convert(Persistence.AuthenticationRequest authenticationRequestStorage)
        {
            return new Session
            {
                id = authenticationRequestStorage.id,
                method = authenticationRequestStorage.method,
                action = authenticationRequestStorage.action,
                token = authenticationRequestStorage.token,
                authorizationId = authenticationRequestStorage.authorizationId,
                extraParams = authenticationRequestStorage.extraParams,
                redirectUrl = authenticationRequestStorage.redirect,
            };
        }
    }
}

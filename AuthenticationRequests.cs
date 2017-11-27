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
    public struct AuthenticationRequest
    {
        public Guid id;
        public CredentialValidationMethodTypes method;
        public string token;
        public Uri loginUrl;
        public Uri redirectUrl;
        public Guid? sessionId;
        public Guid? authorizationId;
        public IDictionary<string, string> extraParams;
        public string refreshToken;
        public AuthenticationActions action;
    }

    public class AuthenticationRequests
    {
        private Context context;
        private Persistence.DataContext dataContext;

        internal AuthenticationRequests(Context context, Persistence.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }
        
        public async Task<TResult> CreateLoginAsync<TResult>(Guid authenticationRequestId, Uri callbackLocation,
                CredentialValidationMethodTypes method, Uri redirectUrl,
            Func<AuthenticationRequest, TResult> onSuccess,
            Func<TResult> onAlreadyExists,
            Func<TResult> onCredentialSystemNotAvailable,
            Func<string,TResult> onCredentialSystemNotInitialized,
            Func<string, TResult> onFailure)
        {
            return await context.GetLoginProvider(method,
                async (provider) =>
                {
                    var sessionId = SecureGuid.Generate();
                    var result = await this.dataContext.AuthenticationRequests.CreateAsync(authenticationRequestId,
                            method, AuthenticationActions.signin, sessionId, default(Guid?), redirectUrl,
                        () => BlackBarLabs.Security.Tokens.JwtTools.CreateToken(sessionId, callbackLocation, TimeSpan.FromMinutes(30),
                            (token) => onSuccess(
                                new AuthenticationRequest()
                                {
                                    id = authenticationRequestId,
                                    method = method,
                                    action = AuthenticationActions.signin,
                                    loginUrl = provider.GetLoginUrl(authenticationRequestId, callbackLocation),
                                    redirectUrl = redirectUrl,
                                    sessionId = sessionId,
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

        public async Task<TResult> CreateLinkAsync<TResult>(Guid authenticationRequestId, 
                Uri callbackLocation,
                CredentialValidationMethodTypes method, Uri redirectUrl,
                Guid authenticationId, Guid actorId, System.Security.Claims.Claim[] claims,
            Func<AuthenticationRequest, TResult> onSuccess,
            Func<TResult> onAlreadyExists,
            Func<string, TResult> onUnauthorized,
            Func<TResult> onCredentialSystemNotAvailable,
            Func<string, TResult> onCredentialSystemNotInitialized,
            Func<string, TResult> onFailure)
        {
            if (authenticationId != actorId)
                return onUnauthorized($"Provided token does not permit access to link {authenticationId} to a login");
            return await context.GetLoginProvider(method,
                async (provider) =>
                {
                    var sessionId = SecureGuid.Generate();
                    var result = await this.dataContext.AuthenticationRequests.CreateAsync(authenticationRequestId,
                            method, AuthenticationActions.link, sessionId, authenticationId, redirectUrl,
                        () => BlackBarLabs.Security.Tokens.JwtTools.CreateToken(sessionId, callbackLocation, TimeSpan.FromMinutes(30),
                            (token) => onSuccess(
                                new AuthenticationRequest()
                                {
                                    id = authenticationRequestId,
                                    method = method,
                                    action = AuthenticationActions.signin,
                                    loginUrl = provider.GetLoginUrl(authenticationRequestId, callbackLocation),
                                    redirectUrl = redirectUrl,
                                    sessionId = sessionId,
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
                Guid sessionId, System.Security.Claims.Claim[] claims,
            Func<AuthenticationRequest, TResult> onSuccess,
            Func<TResult> onNotFound,
            Func<TResult> onUnauthorized,
            Func<string, TResult> onFailure)
        {
            return await this.dataContext.AuthenticationRequests.FindByIdAsync(authenticationRequestId,
                (authenticationRequestStorage) =>
                {
                    if (sessionId != authenticationRequestStorage.sessionId)
                        return onUnauthorized();

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

        public async Task<TResult> UpdateAsync<TResult>(
                Guid sessionId,
                CredentialValidationMethodTypes method,
                IDictionary<string, string> extraParams,
            Func<Guid, Guid, string, string, IDictionary<string, string>, Uri, TResult> onSuccess,
            Func<Guid, TResult> onAlreadyExists,
            Func<string, TResult> onInvalidToken,
            Func<TResult> lookupCredentialNotFound,
            Func<string, TResult> systemOffline,
            Func<string, TResult> onNotConfigured,
            Func<string, TResult> onFailure)
        {
            return await this.context.GetCredentialProvider(method,
                async (provider) =>
                {
                    return await await provider.RedeemTokenAsync(extraParams,
                        async (subject, stateId, loginId, extraParamsWithRedemptionParams) =>
                        {
                            if (stateId.HasValue)
                                return await this.dataContext.AuthenticationRequests.UpdateAsync(stateId.Value,
                                    async (authenticationRequest, saveAuthRequest) =>
                                    {
                                        if (authenticationRequest.method != method)
                                            return onInvalidToken("The credential's authentication method does not match the callback method");

                                        if (AuthenticationActions.link == authenticationRequest.action)
                                        {
                                            if (!authenticationRequest.authorizationId.HasValue)
                                                return onFailure("The credential is corrupt");

                                            var authenticationId = authenticationRequest.authorizationId.Value;
                                            return await await dataContext.CredentialMappings.CreateCredentialMappingAsync(Guid.NewGuid(), method, subject,
                                                    authenticationRequest.authorizationId.Value,
                                                async () => await await context.Sessions.CreateSessionAsync(sessionId, authenticationId,
                                                    async (token, refreshToken) =>
                                                    {
                                                        await saveAuthRequest(authenticationId, token, extraParams);
                                                        return onSuccess(sessionId, authenticationId, token, refreshToken, extraParams,
                                                            authenticationRequest.redirect);
                                                    },
                                                    "GUID not unique".AsFunctionException<Task<TResult>>(),
                                                    onNotConfigured.AsAsyncFunc()),
                                                "GUID not unique".AsFunctionException<Task<TResult>>(),
                                                () => onInvalidToken("Login is already mapped.").ToTask());
                                        }

                                        if (authenticationRequest.authorizationId.HasValue)
                                            return onInvalidToken("AuthenticationRequest cannot be re-used.");
                                        
                                        return await await dataContext.CredentialMappings.LookupCredentialMappingAsync(method, subject, loginId,
                                            async (authenticationId) =>
                                            {
                                                return await await context.Sessions.CreateSessionAsync(sessionId, authenticationId,
                                                    async (token, refreshToken) =>
                                                    {
                                                        await saveAuthRequest(authenticationId, token, extraParams);
                                                        return onSuccess(sessionId, authenticationId, 
                                                            token, refreshToken, extraParams, authenticationRequest.redirect);
                                                    },
                                                    () => onAlreadyExists(sessionId).ToTask(),
                                                    onNotConfigured.AsAsyncFunc());
                                            },
                                            () => onInvalidToken("The token does not match an Authentication request").ToTask());
                                    },
                                    () => onInvalidToken("The token does not match an Authentication request"));
                            return await await dataContext.CredentialMappings.LookupCredentialMappingAsync(method, subject, loginId,
                                (authenticationId) =>
                                {
                                    return context.Sessions.CreateSessionAsync(sessionId, authenticationId,
                                        (token, refreshToken) => onSuccess(sessionId, authenticationId,
                                            token, refreshToken, extraParams, default(Uri)),
                                        () => onAlreadyExists(sessionId),
                                        onNotConfigured);
                                },
                                () => onInvalidToken("The token does not match an Authentication request").ToTask());
                        },
                        onInvalidToken.AsAsyncFunc(),
                        systemOffline.AsAsyncFunc(),
                        onNotConfigured.AsAsyncFunc(),
                        onFailure.AsAsyncFunc());
                },
                () => systemOffline("The requested credential system is not enabled for this deployment").ToTask(),
                (why) => onNotConfigured(why).ToTask());
        }

        internal Task<TResult> DeleteByIdAsync<TResult>(Guid inviteId,
            Guid performingActorId, System.Security.Claims.Claim [] claims,
            Func<TResult> onSuccess, 
            Func<TResult> onNotFound,
            Func<TResult> onUnathorized)
        {
            return this.dataContext.CredentialMappings.DeleteInviteCredentialAsync(inviteId,
                async (current, deleteAsync) =>
                {
                    if (!await Library.configurationManager.CanAdministerCredentialAsync(
                        current.actorId, performingActorId, claims))
                        return onUnathorized();

                    await deleteAsync();
                    return onSuccess();
                },
                onNotFound);
        }
        
        private static AuthenticationRequest Convert(Persistence.AuthenticationRequest authenticationRequestStorage)
        {
            return new AuthenticationRequest
            {
                id = authenticationRequestStorage.id,
                method = authenticationRequestStorage.method,
                action = authenticationRequestStorage.action,
                token = authenticationRequestStorage.token,
                sessionId = authenticationRequestStorage.sessionId,
                authorizationId = authenticationRequestStorage.authorizationId,
                extraParams = authenticationRequestStorage.extraParams,
                redirectUrl = authenticationRequestStorage.redirect,
            };
        }
    }
}

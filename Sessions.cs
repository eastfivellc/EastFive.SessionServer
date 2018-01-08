using System;
using System.Linq;
using System.Threading.Tasks;
using System.Configuration;
using System.Security.Claims;
using System.Collections.Generic;

using BlackBarLabs.Collections.Generic;
using BlackBarLabs.Extensions;
using BlackBarLabs;
using EastFive.Collections.Generic;

namespace EastFive.Security.SessionServer
{
    public struct Session
    {
        public Guid id;
        public CredentialValidationMethodTypes method;
        public string token;
        public Uri loginUrl;
        public Uri logoutUrl;
        public Uri redirectUrl;
        public Uri redirectLogoutUrl;
        public Guid? authorizationId;
        public IDictionary<string, string> extraParams;
        public string refreshToken;
        public AuthenticationActions action;
    }

    public class Sessions
    {
        private Context context;
        private Persistence.DataContext dataContext;

        internal Sessions(Context context, Persistence.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }
        
        public async Task<TResult> CreateLoginAsync<TResult>(Guid authenticationRequestId,
                CredentialValidationMethodTypes method, Uri redirectUrl, Uri redirectLogoutUrl,
                Func<Type, Uri> controllerToLocation,
            Func<Session, TResult> onSuccess,
            Func<TResult> onAlreadyExists,
            Func<TResult> onCredentialSystemNotAvailable,
            Func<string, TResult> onCredentialSystemNotInitialized,
            Func<string, TResult> onFailure)
        {
            return await context.GetLoginProvider(method,
                async (provider) =>
                {
                    var callbackLocation = controllerToLocation(provider.CallbackController);
                    var sessionId = SecureGuid.Generate();
                    var result = await this.dataContext.AuthenticationRequests.CreateAsync(authenticationRequestId,
                            method, AuthenticationActions.signin, default(Guid?), redirectUrl, redirectLogoutUrl,
                        () => BlackBarLabs.Security.Tokens.JwtTools.CreateToken(sessionId, callbackLocation, TimeSpan.FromMinutes(30),
                            (token) => onSuccess(
                                new Session()
                                {
                                    id = authenticationRequestId,
                                    method = method,
                                    action = AuthenticationActions.signin,
                                    loginUrl = provider.GetLoginUrl(authenticationRequestId, callbackLocation),
                                    logoutUrl = provider.GetLogoutUrl(authenticationRequestId, callbackLocation),
                                    redirectUrl = redirectUrl,
                                    redirectLogoutUrl = redirectLogoutUrl,
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

        internal async Task<TResult> CreateSessionAsync<TResult>(Guid sessionId, Guid authenticationId,
            Func<string, string, TResult> onSuccess,
            Func<string, TResult> onConfigurationFailure)
        {
            Func<IDictionary<string, string>, TResult> authenticate =
                (claims) =>
                {
                    var refreshToken = SecureGuid.Generate().ToString("N");
                    var result = GenerateToken(sessionId, authenticationId,
                            claims,
                        (jwtToken) => onSuccess(jwtToken, refreshToken),
                        (why) => onConfigurationFailure(why));
                    return result;
                };
            return await this.context.Claims.FindByAccountIdAsync(authenticationId,
                (claims) => authenticate(claims
                    .Select(claim => new KeyValuePair<string, string>(claim.Type, claim.Value))
                    .ToDictionary()),
                () => authenticate(new Dictionary<string, string>()));
        }
        
        internal async Task<TResult> GetAsync<TResult>(Guid authenticationRequestId, Uri callbackUrl,
            Func<Session, TResult> onSuccess,
            Func<string, TResult> onNotFound,
            Func<string, TResult> onFailure)
        {
            return await this.dataContext.AuthenticationRequests.FindByIdAsync(authenticationRequestId,
                (authenticationRequestStorage) =>
                {
                    if (authenticationRequestStorage.Deleted.HasValue)
                        return onNotFound("Session was deleted");

                    return context.GetLoginProvider(authenticationRequestStorage.method,
                        (provider) =>
                        {
                            var authenticationRequest = Convert(authenticationRequestStorage);
                            authenticationRequest.loginUrl = provider.GetLoginUrl(authenticationRequestId, callbackUrl);
                            authenticationRequest.logoutUrl = provider.GetLogoutUrl(authenticationRequestId, callbackUrl);
                            return onSuccess(authenticationRequest);
                        },
                        () => onFailure("The credential provider for this request is no longer enabled in this system"),
                        (why) => onFailure(why));
                },
                () => onNotFound("Session does not exist"));
        }

        public async Task<TResult> LookupCredentialMappingAsync<TResult>(
                CredentialValidationMethodTypes method, string subject, Guid? loginId, Guid sessionId, 
            IDictionary<string, string> extraParams,
            Func<Guid, string, string, IDictionary<string, string>, TResult> onSuccess,
            Func<TResult> alreadyExists,
            Func<TResult> credentialNotInSystem,
            Func<string, TResult> onConfigurationFailure)
        {
            // Convert authentication unique ID to Actor ID
            var resultLookup = await await dataContext.CredentialMappings.LookupCredentialMappingAsync(method, subject, loginId,
                (actorId) => CreateSessionAsync(sessionId, actorId,
                    (token, refreshToken) => onSuccess(actorId, token, refreshToken, extraParams),
                    onConfigurationFailure),
                () => credentialNotInSystem().ToTask());
            return resultLookup;
        }

        internal async Task<TResult> CreateAsync<TResult>(Guid sessionId, Guid actorId, System.Security.Claims.Claim[] claims,
            Func<string, string, TResult> onSuccess,
            Func<TResult> alreadyExists,
            Func<string, TResult> onConfigurationFailure)
        {
            var refreshToken = EastFive.Security.SecureGuid.Generate().ToString("N");
            var resultFound = await this.dataContext.Sessions.CreateAsync(sessionId, refreshToken, actorId,
                () =>
                {
                    return GenerateToken(sessionId, actorId, claims
                        .Select(claim => new KeyValuePair<string, string>(claim.Type, claim.Value))
                        .ToDictionary(),
                        (jwtToken) => onSuccess(jwtToken, refreshToken),
                        (why) => onConfigurationFailure(why));
                },
                () => alreadyExists());
            return resultFound;
        }
        
        public async Task<TResult> AuthenticateAsync<TResult>(
                Guid sessionId,
                CredentialValidationMethodTypes method,
                IDictionary<string, string> extraParams,
            Func<Guid, Guid, string, string, AuthenticationActions, IDictionary<string, string>, Uri, TResult> onLogin,
            Func<Uri, TResult> onLogout,
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
                                return await AuthenticateStateAsync(sessionId, stateId, loginId, method, subject, extraParamsWithRedemptionParams,
                                    onLogin,
                                    onLogout,
                                    onInvalidToken,
                                    onNotConfigured,
                                    onFailure);

                            return await await dataContext.CredentialMappings.LookupCredentialMappingAsync(method, subject, loginId,
                                (authenticationId) =>
                                {
                                    return context.Sessions.CreateSessionAsync(sessionId, authenticationId,
                                        (token, refreshToken) => onLogin(sessionId, authenticationId,
                                            token, refreshToken, AuthenticationActions.signin, extraParamsWithRedemptionParams,
                                            default(Uri)), // No redirect URL is available since an AuthorizationRequest was not provided
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

        private async Task<TResult> AuthenticateStateAsync<TResult>(Guid sessionId, Guid? stateId, Guid? loginId, CredentialValidationMethodTypes method,
                string subject, IDictionary<string, string> extraParams,
            Func<Guid, Guid, string, string, AuthenticationActions, IDictionary<string, string>, Uri, TResult> onLogin,
            Func<Uri, TResult> onLogout,
            Func<string, TResult> onInvalidToken,
            Func<string, TResult> onNotConfigured,
            Func<string, TResult> onFailure)
        {
            return await this.dataContext.AuthenticationRequests.UpdateAsync(stateId.Value,
                async (authenticationRequest, saveAuthRequest) =>
                {
                    if (authenticationRequest.Deleted.HasValue)
                        return onLogout(authenticationRequest.redirectLogout);

                    if (authenticationRequest.method != method)
                        return onInvalidToken("The credential's authentication method does not match the callback method");

                    if (AuthenticationActions.link == authenticationRequest.action)
                        return await context.Invites.CreateInviteCredentialAsync(sessionId, stateId,
                                authenticationRequest.authorizationId, method, subject,
                                extraParams, saveAuthRequest, authenticationRequest.redirect,
                            onLogin,
                            onInvalidToken,
                            onNotConfigured,
                            onFailure);

                    if (AuthenticationActions.access == authenticationRequest.action)
                        return await context.Integrations.UpdateAsync(authenticationRequest,
                                sessionId, stateId.Value, method, extraParams,
                                saveAuthRequest,
                            onLogin,
                            onInvalidToken,
                            onNotConfigured,
                            onFailure);

                    if (authenticationRequest.authorizationId.HasValue)
                        return onInvalidToken("Session's authentication request cannot be re-used.");

                    return await await dataContext.CredentialMappings.LookupCredentialMappingAsync(method, subject, loginId,
                        async (authenticationId) =>
                        {
                            return await await this.CreateSessionAsync(sessionId, authenticationId,
                                async (token, refreshToken) =>
                                {
                                    await saveAuthRequest(authenticationId, token, extraParams);
                                    return onLogin(stateId.Value, authenticationId,
                                        token, refreshToken, AuthenticationActions.signin, extraParams, authenticationRequest.redirect);
                                },
                                onNotConfigured.AsAsyncFunc());
                        },
                        () => onInvalidToken("The token does not match an Authentication request").ToTask());
                },
                () => onInvalidToken("The token does not match an Authentication request"));
        }

        private TResult GenerateToken<TResult>(Guid sessionId, Guid? actorId, IDictionary<string, string> claims,
            Func<string, TResult> onTokenGenerated,
            Func<string, TResult> onConfigurationIssue)
        {
            var resultExpiration = Web.Configuration.Settings.GetDouble(Configuration.AppSettings.TokenExpirationInMinutes,
                tokenExpirationInMinutes =>
                {
                    return Web.Configuration.Settings.GetString(EastFive.Api.AppSettings.ActorIdClaimType,
                        actorIdClaimType =>
                        {
                            if(actorId.HasValue)
                                claims.AddOrReplace(actorIdClaimType, actorId.ToString());
                            var result = Web.Configuration.Settings.GetUri(AppSettings.TokenScope,
                                (scope) =>
                                {
                                    var jwtToken = BlackBarLabs.Security.Tokens.JwtTools.CreateToken(
                                        sessionId, scope,
                                        TimeSpan.FromMinutes(tokenExpirationInMinutes),
                                        claims,
                                        (token) => token,
                                        (configName) => configName,
                                        (configName, issue) => configName + ":" + issue,
                                        AppSettings.TokenIssuer,
                                        AppSettings.TokenKey);
                                    return onTokenGenerated(jwtToken);
                                },
                                (why) => onConfigurationIssue(why));
                            return result;
                        },
                        (why) => onConfigurationIssue(why));
                },
                (why) => onConfigurationIssue(why));
            return resultExpiration;
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
                redirectLogoutUrl = authenticationRequestStorage.redirectLogout,
            };
        }

        public async Task<TResult> DeleteAsync<TResult>(Guid sessionId,
                Uri callbackLocation,
            Func<Session, TResult> onSuccess,
            Func<TResult> onNotFound,
            Func<string, TResult> onFailure)
        {
            return await this.dataContext.AuthenticationRequests.DeleteAsync(sessionId,
                async (session, markForDeleteAsync) =>
                {
                    return await this.context.GetLoginProvider(session.method,
                        async (provider) =>
                        {
                            session.Deleted = DateTime.UtcNow;
                            await markForDeleteAsync();
                            var deletedSession = Convert(session);
                            deletedSession.logoutUrl = provider.GetLogoutUrl(sessionId, callbackLocation);
                            return onSuccess(deletedSession);
                        },
                        () => onFailure("Credential system is no longer available").ToTask(),
                        (why) => onFailure(why).ToTask());
                },
                onNotFound);
        }
    }
}

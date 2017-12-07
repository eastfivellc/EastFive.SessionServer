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
        
        public async Task<TResult> CreateLoginAsync<TResult>(Guid authenticationRequestId, Uri callbackLocation,
                CredentialValidationMethodTypes method, Uri redirectUrl,
            Func<Session, TResult> onSuccess,
            Func<TResult> onAlreadyExists,
            Func<TResult> onCredentialSystemNotAvailable,
            Func<string, TResult> onCredentialSystemNotInitialized,
            Func<string, TResult> onFailure)
        {
            return await context.GetLoginProvider(method,
                async (provider) =>
                {
                    var sessionId = SecureGuid.Generate();
                    var result = await this.dataContext.AuthenticationRequests.CreateAsync(authenticationRequestId,
                            method, AuthenticationActions.signin, default(Guid?), redirectUrl,
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
            Func<TResult> onSessionAlreadyExists,
            Func<string, TResult> onConfigurationFailure)
        {
            var resultFindByAccount = await await this.context.Claims.FindByAccountIdAsync(authenticationId,
                async (claims) =>
                {
                    var refreshToken = SecureGuid.Generate().ToString("N");
                    var resultFound = await this.dataContext.Sessions.CreateAsync(sessionId, refreshToken, authenticationId,
                        () =>
                        {
                            var result = GenerateToken(sessionId, authenticationId,
                                claims
                                    .Select(claim => new KeyValuePair<string, string>(claim.Type, claim.Value))
                                    .ToDictionary(),
                                (jwtToken) => onSuccess(jwtToken, refreshToken),
                                (why) => onConfigurationFailure(why));
                            return result;
                        },
                        () => onSessionAlreadyExists());
                    return resultFound;
                },
                async () =>
                {
                    var refreshToken = SecureGuid.Generate().ToString("N");
                    var resultFound = await this.dataContext.Sessions.CreateAsync(sessionId, refreshToken, authenticationId,
                        () =>
                        {
                            return GenerateToken(sessionId, authenticationId,
                                    new Dictionary<string, string>(),
                                (jwtToken) => onSuccess(jwtToken, refreshToken),
                                (why) => onConfigurationFailure(why));
                        },
                        () => onSessionAlreadyExists());
                    return resultFound;
                });
            return resultFindByAccount;
        }

        public async Task<TResult> CreateToken<TResult>(Guid actorId, Guid sessionId, Guid actingAsActorId,
            Func<string, string, TResult> onSuccess,
            Func<TResult> onNotAllowed,
            Func<TResult> onAccountNotFound,
            Func<string, TResult> onConfigurationFailure)
        {
            return await EastFive.Web.Configuration.Settings.GetGuid(EastFive.Api.AppSettings.ActorIdSuperAdmin,
                async superAdminActorId =>
                {
                    if (actingAsActorId != superAdminActorId)
                        return onNotAllowed();
                    return await CreateToken(actorId, sessionId,
                        onSuccess, onAccountNotFound, onConfigurationFailure);
                },
                (why) => onConfigurationFailure(why).ToTask());
        }

        public async Task<TResult> CreateToken<TResult>(Guid actorId, Guid sessionId,
            Func<string, string, TResult> onSuccess,
            Func<TResult> onAccountNotFound,
            Func<string, TResult> onConfigurationFailure)
        {
            var resultFindByAccount = await this.context.Claims.FindByAccountIdAsync(actorId,
                        (claims) =>
                        {
                            var refreshToken = SecureGuid.Generate().ToString("N");
                            var result = GenerateToken(sessionId, actorId, claims
                                    .Select(claim => claim.Type.PairWithValue(claim.Value))
                                    .ToDictionary(),
                                (jwtToken) => onSuccess(jwtToken, refreshToken),
                                (why) => onConfigurationFailure(why));
                            return result;
                        },
                        () =>
                        {
                            var refreshToken = SecureGuid.Generate().ToString("N");
                            var result = GenerateToken(sessionId, actorId, new Dictionary<string, string>(),
                                (jwtToken) => onSuccess(jwtToken, refreshToken),
                                (why) => onConfigurationFailure(why));
                            return result;
                        });
            return resultFindByAccount;
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
                    () => alreadyExists(),
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

        public delegate T AuthenticateSuccessDelegate<T>(Guid authorizationId, string token, string refreshToken, IDictionary<string, string> extraParams);
        public delegate T AuthenticateAlreadyAuthenticatedDelegate<T>();
        public delegate T AuthenticateNotFoundDelegate<T>(string message);
        public async Task<T> AuthenticateAsync<T>(Guid sessionId,
            CredentialValidationMethodTypes credentialValidationMethod, Dictionary<string, string> token,
            AuthenticateSuccessDelegate<T> onSuccess,
            Func<string, T> onInvalidCredentials,
            AuthenticateAlreadyAuthenticatedDelegate<T> onAlreadyAuthenticated,
            Func<T> onAuthIdNotFound,
            AuthenticateNotFoundDelegate<T> onNotFound,
            Func<string, T> systemOffline,
            Func<string, T> onUnspecifiedConfiguration,
            Func<string, T> onFailure)
        {
            var updateAuthResult = await this.dataContext.Sessions.UpdateRefreshTokenAsync<T>(sessionId,
                        async (authId, saveAuthId) =>
                        {
                            if (default(Guid) != authId)
                                return onAlreadyAuthenticated();

                            await saveAuthId(sessionId);

                            var claims = new Dictionary<string, string>(); // TODO: load these
                            return GenerateToken(sessionId, authId, claims,
                                jwtToken => onSuccess(authId, jwtToken, string.Empty, new Dictionary<string, string>()),
                                (why) => onUnspecifiedConfiguration(why));
                        },
                        () => onNotFound("Error updating authentication"));
                    return updateAuthResult;
        }
        
        public async Task<TResult> UpdateResponseAsync<TResult>(
                Guid sessionId,
                CredentialValidationMethodTypes method,
                IDictionary<string, string> extraParams,
            Func<Guid, Guid, string, string, AuthenticationActions, IDictionary<string, string>, Uri, TResult> onSuccess,
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

                                            if (typeof(IProvideAccess).IsAssignableFrom(provider.GetType()))
                                                await context.Accesses.CreateAsync(authenticationId, method,
                                                    extraParamsWithRedemptionParams,
                                                    () => true,
                                                    () => false);

                                            return await await dataContext.CredentialMappings.CreateCredentialMappingAsync(Guid.NewGuid(), method, subject,
                                                    authenticationRequest.authorizationId.Value,
                                                async () => await await context.Sessions.CreateSessionAsync(sessionId, authenticationId,
                                                    async (token, refreshToken) =>
                                                    {
                                                        await saveAuthRequest(authenticationId, token, extraParams);
                                                        return onSuccess(stateId.Value, authenticationId, token, refreshToken, AuthenticationActions.link, extraParams,
                                                            authenticationRequest.redirect);
                                                    },
                                                    "GUID not unique".AsFunctionException<Task<TResult>>(),
                                                    onNotConfigured.AsAsyncFunc()),
                                                "GUID not unique".AsFunctionException<Task<TResult>>(),
                                                () => onInvalidToken("Login is already mapped.").ToTask());
                                        }

                                        if (AuthenticationActions.access == authenticationRequest.action)
                                        {
                                            if (!authenticationRequest.authorizationId.HasValue)
                                                return onFailure("The credential is corrupt");

                                            var authenticationId = authenticationRequest.authorizationId.Value;
                                            return await await context.Accesses.CreateAsync(authenticationId, method,
                                                    extraParamsWithRedemptionParams,
                                                    async () => await await context.Sessions.CreateSessionAsync(sessionId, authenticationId,
                                                        async (token, refreshToken) =>
                                                        {
                                                            await saveAuthRequest(authenticationId, token, extraParams);
                                                            return onSuccess(stateId.Value, authenticationId, token, refreshToken,
                                                                AuthenticationActions.access, extraParams,
                                                                authenticationRequest.redirect);
                                                        },
                                                        "GUID not unique".AsFunctionException<Task<TResult>>(),
                                                        onNotConfigured.AsAsyncFunc()),
                                                    () => onInvalidToken("Login is already mapped to an access.").ToTask());
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
                                                        return onSuccess(stateId.Value, authenticationId,
                                                            token, refreshToken, AuthenticationActions.signin, extraParams, authenticationRequest.redirect);
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
                                            token, refreshToken, AuthenticationActions.signin, extraParams, default(Uri)),
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

        private TResult GenerateToken<TResult>(Guid sessionId, Guid? actorId, IDictionary<string, string> claims,
            Func<string, TResult> onTokenGenerated,
            Func<string, TResult> onConfigurationIssue)
        {
            var resultExpiration = Web.Configuration.Settings.GetDouble(Configuration.AppSettings.TokenExpirationInMinutes,
                tokenExpirationInMinutes =>
                {
                    return Web.Configuration.Settings.GetString(EastFive.Api.Configuration.SecurityDefinitions.ActorIdClaimType,
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
            };
        }
    }
}

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
using Microsoft.ApplicationInsights;
using EastFive.Linq;

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
        public IDictionary<string, CustomParameter> userParams;
        public string refreshToken;
        public AuthenticationActions action;
    }

    public struct CustomParameter
    {
        public string Value;
        public string Label;
        public Type Type;
        public string Description;
    }

    public class Sessions
    {
        private Context context;
        private Persistence.DataContext dataContext;
        private TelemetryClient telemetry;

        internal Sessions(Context context, Persistence.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;

            telemetry = Web.Configuration.Settings.GetString(SessionServer.Configuration.AppSettings.ApplicationInsightsKey,
                (applicationInsightsKey) =>
                {
                    return new TelemetryClient { InstrumentationKey = applicationInsightsKey };
                },
                (why) =>
                {
                    return new TelemetryClient();
                });
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
                            method, AuthenticationActions.signin, redirectUrl, redirectLogoutUrl,
                        () => BlackBarLabs.Security.Tokens.JwtTools.CreateToken(sessionId, callbackLocation, TimeSpan.FromMinutes(30),
                            (token) =>
                            {
                                var session = new Session()
                                {
                                    id = authenticationRequestId,
                                    method = method,
                                    action = AuthenticationActions.signin,
                                    loginUrl = provider.GetLoginUrl(authenticationRequestId, callbackLocation),
                                    logoutUrl = provider.GetLogoutUrl(authenticationRequestId, callbackLocation),
                                    redirectUrl = redirectUrl,
                                    redirectLogoutUrl = redirectLogoutUrl,
                                    token = token,
                                };
                                return onSuccess(session);
                            },
                            why => onFailure(why),
                            (param, why) => onFailure($"Invalid configuration for {param}:{why}")),
                        onAlreadyExists);
                    return result;
                },
                onCredentialSystemNotAvailable.AsAsyncFunc(),
                onCredentialSystemNotInitialized.AsAsyncFunc());
        }

        internal async Task<TResult> CreateLoginAsync<TResult>(Guid authenticationRequestId, Guid authenticationId,
                CredentialValidationMethodTypes method, Uri callbackLocation, IDictionary<string, string> authParams,
            Func<Session, TResult> onSuccess,
            Func<TResult> onAlreadyExists,
            Func<string, TResult> onFailure)
        {
            return await EastFive.Web.Configuration.Settings.GetUri(
                EastFive.Security.AppSettings.TokenScope,
                async (scope) =>
                {
                    var sessionId = SecureGuid.Generate();
                    var claims = await this.context.Claims.FindByAccountIdAsync(authenticationId,
                        (cs) => cs.Select(c => c.Type.PairWithValue(c.Value)).ToDictionary(),
                        () => new Dictionary<string, string>());
                    return await Sessions.GenerateToken(sessionId, authenticationId, claims,
                                (token) => this.dataContext.AuthenticationRequests.CreateAsync(authenticationRequestId,
                                        method, AuthenticationActions.signin, authenticationId, token, callbackLocation, callbackLocation,
                                    () =>
                                    {
                                        telemetry.TrackEvent("Sessions.CreateLoginAsync - Create Session", authParams);
                                        var session = new Session()
                                        {
                                            id = authenticationRequestId,
                                            method = method,
                                            action = AuthenticationActions.signin,
                                            token = token,
                                            extraParams = authParams
                                        };
                                        return onSuccess(session);
                                    },
                                    onAlreadyExists),
                            why => onFailure(why).ToTask());
                },
                onFailure.AsAsyncFunc());
        }

        internal async Task<TResult> GenerateSessionWithClaimsAsync<TResult>(Guid sessionId, Guid authenticationId,
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
        
        internal async Task<TResult> GetAsync<TResult>(Guid authenticationRequestId, Func<Type, Uri> callbackUrlFunc,
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
                            var callbackUrl = callbackUrlFunc(provider.CallbackController);
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
                (actorId) => GenerateSessionWithClaimsAsync(sessionId, actorId,
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
        
        public Task<TResult> UpdateAsync<TResult>(Guid authenticationRequestId, Guid actingAsUser, System.Security.Claims.Claim[] claims,
            IDictionary<string, string> updatedUserParameters,
            Func<TResult> onUpdated,
            Func<Guid, Guid, string, string, AuthenticationActions, IDictionary<string, string>, Uri, TResult> onLogin,
            Func<Uri, TResult> onLogout,
            Func<string, TResult> onInvalidToken,
            Func<TResult> onLookupCredentialNotFound,
            Func<string, TResult> onSystemOffline,
            Func<string, TResult> onNotConfigured,
            Func<string, TResult> onFailure)
        {
            return dataContext.AuthenticationRequests.UpdateAsync(authenticationRequestId, 
                async (authRequestStorage, saveAsync) =>
                {
                    if (!authRequestStorage.authorizationId.HasValue)
                    {
                        return await UpdateWithAuthenticationAsync(authenticationRequestId, authRequestStorage.method, updatedUserParameters,
                            onLogin,
                            onLogout,
                            onInvalidToken,
                            onLookupCredentialNotFound,
                            onSystemOffline,
                            onNotConfigured,
                            onFailure);
                    }

                    return await context.GetLoginProvider(authRequestStorage.method,
                        async (provider) =>
                        {
                            var userHash = await provider.UserParametersAsync(actingAsUser, claims, authRequestStorage.extraParams,
                                (labels, types, descriptions) =>
                                {
                                    return labels.SelectKeys().Concat(types.SelectKeys()).Concat(descriptions.SelectKeys())
                                        .Distinct()
                                        .AsHashSet();
                                });

                            var mergedExtraParams = authRequestStorage.extraParams
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
                            await saveAsync(authRequestStorage.authorizationId.Value, authRequestStorage.token, mergedExtraParams);
                            return onUpdated();
                        },
                        ()=> onFailure("Integration is no longer available for the system given").ToTask(),
                        onFailure.AsAsyncFunc());
                },
                onLookupCredentialNotFound);
        }

        public async Task<TResult> UpdateWithAuthenticationAsync<TResult>(
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
                            {
                                if (stateId.Value != sessionId)
                                    return onInvalidToken("The authorization flow did not match this resource");

                                return await AuthenticateStateAsync(stateId.Value, loginId, method, subject, extraParamsWithRedemptionParams,
                                    onLogin,
                                    onLogout,
                                    onInvalidToken,
                                    onNotConfigured,
                                    onFailure);
                            }

                            return await await dataContext.CredentialMappings.LookupCredentialMappingAsync(method, subject, loginId,
                                (authenticationId) =>
                                {
                                    return context.Sessions.GenerateSessionWithClaimsAsync(sessionId, authenticationId,
                                        (token, refreshToken) => onLogin(sessionId, authenticationId,
                                            token, refreshToken, AuthenticationActions.signin, extraParamsWithRedemptionParams,
                                            default(Uri)), // No redirect URL is available since an AuthorizationRequest was not provided
                                        onNotConfigured);
                                },
                                () => onInvalidToken("The token does not map to a user in this system.").ToTask());
                        },
                        async (stateId, extraParamsWithRedemptionParams) =>
                        {
                            if (!stateId.HasValue)
                                onLogout(default(Uri));

                            return await dataContext.AuthenticationRequests.FindByIdAsync(stateId.Value,
                                (authRequest) => onLogout(authRequest.redirectLogout),
                                () => onLogout(default(Uri)));
                        },
                        onInvalidToken.AsAsyncFunc(),
                        systemOffline.AsAsyncFunc(),
                        onNotConfigured.AsAsyncFunc(),
                        onFailure.AsAsyncFunc());
                },
                () => systemOffline("The requested credential system is not enabled for this deployment").ToTask(),
                (why) => onNotConfigured(why).ToTask());
        }

        public async Task<TResult> CreateOrUpdateWithAuthenticationAsync<TResult>(
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
                            telemetry.TrackEvent("Sessions.CreateOrUpdateWithAuthenticationAsync:  Authenticated", extraParamsWithRedemptionParams);
                            // This is the case where the login process started from an existing authentication resource
                            if (stateId.HasValue)
                            {
                                telemetry.TrackEvent($"Sessions.CreateOrUpdateWithAuthenticationAsync:  StateId: {stateId.Value.ToString()}");
                                return await AuthenticateStateAsync(stateId.Value, loginId, method, subject, extraParamsWithRedemptionParams,
                                    onLogin,
                                    onLogout,
                                    onInvalidToken,
                                    onNotConfigured,
                                    onFailure);
                            }
                            // This is the case where the login process started from an external system
                            telemetry.TrackEvent("StateId not found.  Starting external system login flow.");
                            return await await dataContext.CredentialMappings.LookupCredentialMappingAsync(method, subject, loginId,
                                (authenticationId) =>
                                {
                                    telemetry.TrackEvent($"Sessions.CreateOrUpdateWithAuthenticationAsync:  Called from external login system.  AuthenticationId: {authenticationId.ToString()}");
                                    var authorizationId = Guid.NewGuid();
                                    return this.CreateLoginAsync(authorizationId, authenticationId, method, default(Uri), extraParamsWithRedemptionParams,
                                        (session) => onLogin(authorizationId, authenticationId, session.token, session.refreshToken, AuthenticationActions.signin, session.extraParams,
                                                default(Uri)),
                                        "Guid not unique for creating authentication started from external system".AsFunctionException<TResult>(),
                                        onFailure);
                                },
                                () => onInvalidToken("The token does not map to a user in this system.").ToTask());
                        },
                        async (stateId, extraParamsWithRedemptionParams) =>
                        {
                            telemetry.TrackEvent("Sessions.CreateOrUpdateWithAuthenticationAsync:  Not Authenticated");
                            if (!stateId.HasValue)
                                onLogout(default(Uri));

                            return await dataContext.AuthenticationRequests.FindByIdAsync(stateId.Value,
                                (authRequest) => onLogout(authRequest.redirectLogout),
                                () => onLogout(default(Uri)));
                        },
                        onInvalidToken.AsAsyncFunc(),
                        systemOffline.AsAsyncFunc(),
                        onNotConfigured.AsAsyncFunc(),
                        onFailure.AsAsyncFunc());
                },
                () => systemOffline("The requested credential system is not enabled for this deployment").ToTask(),
                (why) => onNotConfigured(why).ToTask());
        }


        private async Task<TResult> AuthenticateStateAsync<TResult>(Guid sessionId, Guid? loginId, CredentialValidationMethodTypes method,
                string subject, IDictionary<string, string> extraParams,
            Func<Guid, Guid, string, string, AuthenticationActions, IDictionary<string, string>, Uri, TResult> onLogin,
            Func<Uri, TResult> onLogout,
            Func<string, TResult> onInvalidToken,
            Func<string, TResult> onNotConfigured,
            Func<string, TResult> onFailure)
        {
            return await this.dataContext.AuthenticationRequests.UpdateAsync(sessionId,
                async (authenticationRequest, saveAuthRequest) =>
                {
                    if (authenticationRequest.Deleted.HasValue)
                        return onLogout(authenticationRequest.redirectLogout);

                    if (authenticationRequest.method != method)
                        return onInvalidToken("The credential's authentication method does not match the callback method");

                    if (AuthenticationActions.link == authenticationRequest.action)
                        return await context.Invites.CreateInviteCredentialAsync(sessionId, sessionId,
                                authenticationRequest.authorizationId, method, subject,
                                extraParams, saveAuthRequest, authenticationRequest.redirect,
                            onLogin,
                            onInvalidToken,
                            onNotConfigured,
                            onFailure);

                    if (AuthenticationActions.access == authenticationRequest.action)
                        return await await context.Integrations.SetAsAuthenticatedAsync(authenticationRequest,
                                sessionId, method, extraParams,
                            async (authenticationId, token, refreshToken, redirect) =>
                            {
                                await saveAuthRequest(authenticationId, token, extraParams);
                                return onLogin(sessionId, authenticationId, token, refreshToken, AuthenticationActions.access, extraParams, redirect);
                            },
                            () => onInvalidToken("Token has already been used to gain access").ToTask(),
                            onNotConfigured.AsAsyncFunc(),
                            onFailure.AsAsyncFunc());

                    if (authenticationRequest.authorizationId.HasValue)
                        return onInvalidToken("Session's authentication request cannot be re-used.");

                    return await await dataContext.CredentialMappings.LookupCredentialMappingAsync(method, subject, loginId,
                        async (authenticationId) =>
                        {
                            return await await this.GenerateSessionWithClaimsAsync(sessionId, authenticationId,
                                async (token, refreshToken) =>
                                {
                                    await saveAuthRequest(authenticationId, token, extraParams);
                                    return onLogin(sessionId, authenticationId,
                                        token, refreshToken, AuthenticationActions.signin, extraParams, authenticationRequest.redirect);
                                },
                                onNotConfigured.AsAsyncFunc());
                        },
                        () => onInvalidToken("The token does not match a user in this system.").ToTask());
                },
                () => onInvalidToken("The token does not match an Authentication request"));
        }

        private static TResult GenerateToken<TResult>(Guid sessionId, Guid? actorId, IDictionary<string, string> claims,
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
                Func<Type, Uri> callbackLocationFunc,
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
                            var callbackLocation = callbackLocationFunc(provider.CallbackController);
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

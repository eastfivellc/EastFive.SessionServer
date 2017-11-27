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
    public class Sessions
    {
        private Context context;
        private Persistence.DataContext dataContext;

        internal Sessions(Context context, Persistence.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }

        public delegate T CreateSessionSuccessDelegate<T>(Guid? authorizationId, string token, string refreshToken, IDictionary<string, string> extraParams);
        public delegate T CreateSessionAlreadyExistsDelegate<T>();
        public async Task<T> CreateAsync<T>(Guid sessionId,
            CreateSessionSuccessDelegate<T> onSuccess,
            CreateSessionAlreadyExistsDelegate<T> alreadyExists,
            Func<string, T> onFailure)
        {
            var refreshToken = EastFive.Security.SecureGuid.Generate().ToString("N");
            return await this.dataContext.Sessions.CreateAsync(sessionId, refreshToken, default(Guid),
                () =>
                {
                    var result = this.GenerateToken(sessionId, default(Guid), new Dictionary<string, string>(),
                        (jwtToken) => onSuccess(default(Guid), jwtToken, refreshToken, new Dictionary<string, string>()),
                        (why) => onFailure(why));
                    return result;
                },
                () => alreadyExists());
        }

        public async Task<TResult> CreateAsync<TResult>(Guid sessionId,
            Func<string, string, TResult> onSuccess,
            Func<Guid, TResult> onSessionAlreadyExists,
            Func<string, TResult> onNotConfigured,
            Func<string, TResult> onFailure)
        {
            var refreshToken = SecureGuid.Generate().ToString("N");
            return await this.dataContext.Sessions.CreateAsync(sessionId, refreshToken,
                () =>
                {
                    var result = GenerateToken(sessionId, default(Guid?),
                        new Dictionary<string, string>(),
                        (jwtToken) => onSuccess(jwtToken, refreshToken),
                        onNotConfigured);
                    return result;
                },
                () => onSessionAlreadyExists(sessionId));
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

        public async Task<TResult> LookupCredentialMappingAsync<TResult>(
                CredentialValidationMethodTypes method, string subject, Guid? loginId, Guid sessionId, 
            IDictionary<string, string> extraParams,
            Func<Guid, string, string, IDictionary<string, string>, TResult> onSuccess,
            CreateSessionAlreadyExistsDelegate<TResult> alreadyExists,
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
    }
}

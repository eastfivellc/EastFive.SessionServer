using System;
using System.Linq;
using System.Threading.Tasks;
using System.Configuration;
using System.Security.Claims;
using System.Collections.Generic;

using BlackBarLabs.Collections.Generic;
using BlackBarLabs.Extensions;

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
            CreateSessionAlreadyExistsDelegate<T> alreadyExists)
        {
            var refreshToken = EastFive.Security.SecureGuid.Generate().ToString("N");
            return await this.dataContext.Sessions.CreateAsync(sessionId, refreshToken, default(Guid),
                () =>
                {
                    var jwtToken = this.GenerateToken(sessionId, default(Guid), new Dictionary<string, string>());
                    return onSuccess.Invoke(default(Guid), jwtToken, refreshToken, new Dictionary<string, string>());
                },
                () => alreadyExists());
        }

        public async Task<T> CreateAsync<T>(Guid sessionId,
            CredentialValidationMethodTypes method, string token,
            CreateSessionSuccessDelegate<T> onSuccess,
            CreateSessionAlreadyExistsDelegate<T> alreadyExists,
            Func<string, T> invalidToken,
            Func<T> authIdNotFound,
            Func<T> credentialNotInSystem,
            Func<string, T> systemOffline)
        {
            var result = await await AuthenticateCredentialsAsync(method, token,
                async (authorizationId, extraParams) =>
                {
                    // Convert authentication unique ID to Actor ID
                    var inner = await await dataContext.CredentialMappings.LookupCredentialMappingAsync<Task<T>>(authorizationId,
                        async (actorId) =>
                        {
                            var refreshToken = EastFive.Security.SecureGuid.Generate().ToString("N");
                            var resultFound = await await this.dataContext.Sessions.CreateAsync(sessionId, refreshToken, actorId,
                                async() =>
                                {
                                    // LOAD CLAIMS FROM IDENTITY SYSTEM HERE
                                    var claimResult = await this.context.Claims.FindByAccountIdAsync(actorId,
                                        (customClaims) =>
                                        {
                                            return GenerateToken(sessionId, actorId, customClaims
                                                .Select(claim => new KeyValuePair<string, string>(claim.Type, claim.Value))
                                                .ToDictionary(),
                                                (jwtToken) => onSuccess(actorId, jwtToken, refreshToken, extraParams),
                                                (why) => systemOffline(why));
                                        },
                                        () => credentialNotInSystem());
                                    return claimResult;
                                },
                                () => alreadyExists().ToTask());
                            return resultFound;
                        },
                        () => credentialNotInSystem().ToTask());
                    return inner;
                },
                (why) => invalidToken(why).ToTask(),
                () => authIdNotFound().ToTask(),
                (why) => systemOffline(why).ToTask());
            return result;
        }

        public async Task<T> CreateAsync<T>(Guid sessionId,
            CredentialValidationMethodTypes method, string token, string state,
            CreateSessionSuccessDelegate<T> onSuccess,
            CreateSessionAlreadyExistsDelegate<T> alreadyExists,
            Func<string, T> invalidToken,
            Func<string, T> invalidState,
            Func<T> authIdNotFound,
            Func<Guid, T> credentialNotInSystem,
            Func<T> lookupCredentialNotFound,
            Func<T> alreadyRedeemed,
            Func<T> onAlreadyInUse,
            Func<string, T> systemOffline,
            Func<string, T> onNotConfigured)
        {
            var loginProvider = await this.context.LoginProvider;
            var parseResult = await loginProvider.ParseState(state,
                async (action, data, extraParamsFromState) =>
                {
                    var result = await await AuthenticateCredentialsAsync(method, token,
                        async (loginId, extraParamsFromCredetial) =>
                        {
                            var extraParams = extraParamsFromState.Concat(extraParamsFromCredetial).ToDictionary();

                            if (action == 1)
                                return await CreateWithNewNewAccountAsync(loginId, sessionId, data, extraParams,
                                    onSuccess, alreadyExists, lookupCredentialNotFound, alreadyRedeemed,
                                    onAlreadyInUse, onAlreadyInUse);

                            return await LookupCredentialMappingAsync(loginId, sessionId, new Dictionary<string, string>(),
                                (authId, jwtToken, refreshToken, extraParamsPlus) => onSuccess(authId, jwtToken, refreshToken,
                                    extraParams.Concat(extraParamsPlus).ToDictionary()),
                                alreadyExists, () => credentialNotInSystem(loginId), onNotConfigured);
                        },
                        (why) => invalidToken(why).ToTask(),
                        () => authIdNotFound().ToTask(),
                        (why) => systemOffline(why).ToTask());
                    return result;
                },
                (why) => invalidState(why).ToTask());
            return parseResult;
        }

        public async Task<T> CreateAsync<T>(Guid sessionId,
            CredentialValidationMethodTypes method, string token,
            CreateSessionSuccessDelegate<T> onSuccess,
            CreateSessionAlreadyExistsDelegate<T> alreadyExists,
            Func<string, T> invalidToken,
            Func<T> authIdNotFound,
            Func<T> lookupCredentialNotFound,
            Func<string, T> systemOffline,
            Func<string, T> onNotConfigured)
        {
            var loginProvider = await this.context.LoginProvider;
            var result = await await AuthenticateCredentialsAsync(method, token,
                async (loginId, extraParams) =>
                {
                    var refreshToken = SecureGuid.Generate().ToString("N"); // TODO: Store this
                    return await LookupCredentialMappingAsync(loginId, sessionId, extraParams,
                        onSuccess, alreadyExists, lookupCredentialNotFound, onNotConfigured);
                },
                (why) => invalidToken(why).ToTask(),
                () => authIdNotFound().ToTask(),
                (why) => systemOffline(why).ToTask());
            return result;
        }

        private async Task<TResult> CreateWithNewNewAccountAsync<TResult>(Guid loginId, Guid sessionId,
            byte [] data, IDictionary<string, string> extraParamsFromCall,
            CreateSessionSuccessDelegate<TResult> onSuccess,
            CreateSessionAlreadyExistsDelegate<TResult> alreadyExists,
            Func<TResult> lookupCredentialNotFound,
            Func<TResult> alreadyRedeemed,
            Func<TResult> onAlreadyInUse,
            Func<TResult> onAlreadyConnected)
        {
            var inviteToken = new Guid(data);
            return await await this.dataContext.CredentialMappings.MarkInviteRedeemedAsync(inviteToken,
                loginId,
                async (actorId) =>
                {
                    var refreshToken = EastFive.Security.SecureGuid.Generate().ToString("N");
                    var extraParamsFromInvite = new Dictionary<string, string>(); // TODO: Get this from the invite redemptions


                    var resultCreated = await await this.dataContext.Sessions.CreateAsync(sessionId, refreshToken, actorId,
                        async () =>
                        {
                            var extraParams = extraParamsFromCall.Concat(extraParamsFromInvite).ToDictionary();
                            var resultFound = await this.context.Claims.FindByAccountIdAsync(actorId,
                                (claims) =>
                                {
                                    var jwtToken = GenerateToken(sessionId, actorId, claims
                                            .Select(claim => new KeyValuePair<string, string>(claim.Type, claim.Value))
                                            .ToDictionary());
                                    return onSuccess(actorId, jwtToken, refreshToken, extraParams);
                                },
                                () =>
                                {
                                    var claims = new Dictionary<string, string>();
                                    var jwtToken = GenerateToken(sessionId, actorId, claims);
                                    return onSuccess(actorId, jwtToken, refreshToken, extraParams);
                                });
                            return resultFound;
                        },
                        () => alreadyExists().ToTask());
                    return resultCreated;
                        
                },
                () => lookupCredentialNotFound().ToTask(),
                (connectedActorId) => alreadyRedeemed().ToTask(),
                () => onAlreadyInUse().ToTask(),
                () => onAlreadyConnected().ToTask());
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
                        onAccountNotFound);
            return resultFindByAccount;
        }

        public async Task<TResult> LookupCredentialMappingAsync<TResult>(Guid loginId, Guid sessionId, 
            IDictionary<string, string> extraParams,
            CreateSessionSuccessDelegate<TResult> onSuccess,
            CreateSessionAlreadyExistsDelegate<TResult> alreadyExists,
            Func<TResult> credentialNotInSystem,
            Func<string, TResult> onConfigurationFailure)
        {
            // Convert authentication unique ID to Actor ID
            var resultLookup = await await dataContext.CredentialMappings.LookupCredentialMappingAsync(loginId,
                async (actorId) =>
                {
                    var resultFindByAccount = await await this.context.Claims.FindByAccountIdAsync(actorId,
                        async (claims) =>
                        {
                            var refreshToken = EastFive.Security.SecureGuid.Generate().ToString("N");
                            var resultFound = await this.dataContext.Sessions.CreateAsync(sessionId, refreshToken, actorId,
                                () =>
                                {
                                    var result = GenerateToken(sessionId, actorId, claims
                                            .Select(claim => new KeyValuePair<string, string>(claim.Type, claim.Value))
                                            .ToDictionary(),
                                            (jwtToken) => onSuccess(actorId, jwtToken, refreshToken, extraParams),
                                            (why) => onConfigurationFailure(why));
                                    return result;
                                },
                                () => alreadyExists());
                            return resultFound;
                        },
                        async () =>
                        {
                            var refreshToken = EastFive.Security.SecureGuid.Generate().ToString("N");
                            var resultFound = await this.dataContext.Sessions.CreateAsync(sessionId, refreshToken, actorId,
                                () =>
                                {
                                    return GenerateToken(sessionId, actorId,
                                        new Dictionary<string, string>(),
                                        (jwtToken) => onSuccess(actorId, jwtToken, refreshToken, extraParams),
                                        (why) => onConfigurationFailure(why));
                                },
                                () => alreadyExists());
                            return resultFound;
                        });
                    return resultFindByAccount;
                },
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
            CredentialValidationMethodTypes credentialValidationMethod, string token,
            AuthenticateSuccessDelegate<T> onSuccess,
            Func<string, T> onInvalidCredentials,
            AuthenticateAlreadyAuthenticatedDelegate<T> onAlreadyAuthenticated,
            Func<T> onAuthIdNotFound,
            AuthenticateNotFoundDelegate<T> onNotFound,
            Func<string, T> systemOffline)
        {
            var result = await await AuthenticateCredentialsAsync(credentialValidationMethod, token,
                async (authorizationId, extraParams) =>
                {
                    var updateAuthResult = await this.dataContext.Sessions.UpdateAuthentication<T>(sessionId,
                        async (authId, saveAuthId) =>
                        {
                            if (default(Guid) != authId)
                                return onAlreadyAuthenticated();

                            await saveAuthId(authorizationId);

                            var claims = new Dictionary<string, string>(); // TODO: load these
                            return GenerateToken(sessionId, authorizationId, claims,
                                jwtToken => onSuccess.Invoke(authorizationId, jwtToken, string.Empty, extraParams),
                                (why) => systemOffline(why));
                        },
                        () => onNotFound("Error updating authentication"));
                    return updateAuthResult;
                },
                (why) => onInvalidCredentials(why).ToTask(),
                () => onAuthIdNotFound().ToTask(),
                (why) => systemOffline(why).ToTask());
            return result;
        }

        private async Task<T> AuthenticateCredentialsAsync<T>(
            CredentialValidationMethodTypes method, string token,
            Func<Guid, IDictionary<string, string>, T> onSuccess, 
            Func<string, T> onInvalidCredential,
            Func<T> onAuthIdNotFound,
            Func<string, T> systemUnavailable)
        {
            var provider = await this.context.GetCredentialProvider(method);
            return await provider.RedeemTokenAsync(token,
                (authorizationId, extraParams) =>
                {
                    return onSuccess(authorizationId, extraParams);
                },
                (why) => onInvalidCredential(why),
                () => onAuthIdNotFound(),
                (why) => systemUnavailable(why));
        }

        //private string GenerateToken(Guid sessionId, Guid authorizationId, SessionServer.Persistence.Claim [] claims)
        //{
        //    var jwtClaims = GetClaims(sessionId, authorizationId, claims);
        //    return GenerateToken(sessionId, authorizationId, jwtClaims);
        //}

        [Obsolete]
        private string GenerateToken(Guid sessionId, Guid actorId, IDictionary<string, string> claims)
        {
            var tokenExpirationInMinutesConfig = ConfigurationManager.AppSettings[Configuration.AppSettings.TokenExpirationInMinutes];
            if (string.IsNullOrEmpty(tokenExpirationInMinutesConfig))
                throw new SystemException("TokenExpirationInMinutes was not found in the configuration file");
            var tokenExpirationInMinutes = Double.Parse(tokenExpirationInMinutesConfig);

            var actorIdClaimType = ConfigurationManager.AppSettings[EastFive.Api.Configuration.SecurityDefinitions.ActorIdClaimType];
            claims.AddOrReplace(actorIdClaimType, actorId.ToString());

            var jwtToken = BlackBarLabs.Security.Tokens.JwtTools.CreateToken(
                sessionId, Web.Configuration.Settings.GetUri(AppSettings.TokenScope),
                TimeSpan.FromMinutes(tokenExpirationInMinutes),
                claims,
                (token) => token,
                (configName) => configName,
                (configName, issue) => configName + ":" + issue,
                AppSettings.TokenIssuer,
                AppSettings.TokenKey);

            return jwtToken;
        }

        private TResult GenerateToken<TResult>(Guid sessionId, Guid actorId, IDictionary<string, string> claims,
            Func<string, TResult> onTokenGenerated,
            Func<string, TResult> onConfigurationIssue)
        {
            var resultExpiration = Web.Configuration.Settings.GetDouble(Configuration.AppSettings.TokenExpirationInMinutes,
                tokenExpirationInMinutes =>
                {
                    return Web.Configuration.Settings.GetString(EastFive.Api.Configuration.SecurityDefinitions.ActorIdClaimType,
                        actorIdClaimType =>
                        {
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

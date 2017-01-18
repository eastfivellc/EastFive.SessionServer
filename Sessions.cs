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
        private Persistence.Azure.DataContext dataContext;

        internal Sessions(Context context, Persistence.Azure.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }

        public delegate T CreateSessionSuccessDelegate<T>(Uri redirectUrl, Guid authorizationId, string token, string refreshToken);
        public delegate T CreateSessionAlreadyExistsDelegate<T>();
        public async Task<T> CreateAsync<T>(Guid sessionId,
            CreateSessionSuccessDelegate<T> onSuccess,
            CreateSessionAlreadyExistsDelegate<T> alreadyExists)
        {
            var refreshToken = BlackBarLabs.Security.SecureGuid.Generate().ToString("N");
            return await this.dataContext.Sessions.CreateAsync(sessionId, refreshToken, default(Guid),
                () =>
                {
                    var jwtToken = this.GenerateToken(sessionId, default(Guid), new Dictionary<string, string>());
                    return onSuccess.Invoke(default(Uri), default(Guid), jwtToken, refreshToken);
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
                        async (authorizationId, claims) =>
                        {
                            // Convert authentication unique ID to Actor ID
                            return await await dataContext.Authorizations.LookupCredentialMappingAsync(authorizationId,
                                async (actorId) =>
                                {
                                    var refreshToken = BlackBarLabs.Security.SecureGuid.Generate().ToString("N");
                                    var resultFound = await this.dataContext.Sessions.CreateAsync(sessionId, refreshToken, actorId,
                                        () =>
                                        {
                                            var jwtToken = GenerateToken(sessionId, actorId, claims
                                                .Select(claim => new KeyValuePair<string, string>(claim.Type, claim.Value))
                                                .ToDictionary());
                                            return onSuccess(default(Uri), actorId, jwtToken, refreshToken);
                                        },
                                        () => alreadyExists());
                                    return resultFound;
                                },
                                () => credentialNotInSystem().ToTask());
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
            Func<T> credentialNotInSystem,
            Func<T> lookupCredentialNotFound,
            Func<T> alreadyRedeemed,
            Func<string, T> systemOffline)
        {
            var loginProvider = await this.context.LoginProvider;
            var parseResult = await loginProvider.ParseState(state,
                async (redirectUri, action, data) =>
                {
                    var result = await await AuthenticateCredentialsAsync(method, token,
                        async (loginId, claims) =>
                        {
                            if (action == 1)
                                return await CreateLookupCredentialMappingAsync(loginId, sessionId, data,
                                    claims, redirectUri,
                                    onSuccess, alreadyExists, lookupCredentialNotFound, alreadyRedeemed);

                            return await LookupCredentialMappingAsync(loginId, sessionId,
                                claims, redirectUri,
                                onSuccess, alreadyExists, credentialNotInSystem);
                        },
                        (why) => invalidToken(why).ToTask(),
                        () => authIdNotFound().ToTask(),
                        (why) => systemOffline(why).ToTask());
                    return result;
                },
                (why) => invalidState(why).ToTask());
            return parseResult;
        }

        private async Task<TResult> CreateLookupCredentialMappingAsync<TResult>(Guid loginId, Guid sessionId,
            byte [] data,
            System.Security.Claims.Claim[] claims, Uri redirectUri,
            CreateSessionSuccessDelegate<TResult> onSuccess,
            CreateSessionAlreadyExistsDelegate<TResult> alreadyExists,
            Func<TResult> lookupCredentialNotFound,
            Func<TResult> alreadyRedeemed)
        {
            var redirectId = new Guid(data);
            return await await this.dataContext.Authorizations.MarkCredentialRedirectAsync(redirectId,
                async (actorId) =>
                {
                    Func<Task<TResult>> createSession = 
                    async () =>
                    {
                        var refreshToken = BlackBarLabs.Security.SecureGuid.Generate().ToString("N");
                        var resultFound = await this.dataContext.Sessions.CreateAsync(sessionId, refreshToken, actorId,
                            () =>
                            {
                                var jwtToken = GenerateToken(sessionId, actorId, claims
                                    .Select(claim => new KeyValuePair<string, string>(claim.Type, claim.Value))
                                    .ToDictionary());
                                return onSuccess(redirectUri, actorId, jwtToken, refreshToken);
                            },
                            () => alreadyExists());
                        return resultFound;
                    };
                    return await await dataContext.Authorizations.CreateCredentialAsync(loginId, actorId,
                        createSession, createSession);
                },
                () => lookupCredentialNotFound().ToTask(),
                (connectedActorId) => alreadyRedeemed().ToTask());
        }

        private async Task<TResult> LookupCredentialMappingAsync<TResult>(Guid authorizationId, Guid sessionId,
            System.Security.Claims.Claim[] claims, Uri redirectUri,
            CreateSessionSuccessDelegate<TResult> onSuccess,
            CreateSessionAlreadyExistsDelegate<TResult> alreadyExists,
            Func<TResult> credentialNotInSystem)
        {
            // Convert authentication unique ID to Actor ID
            var result = await await dataContext.Authorizations.LookupCredentialMappingAsync(authorizationId,
                async (actorId) =>
                {
                    var refreshToken = BlackBarLabs.Security.SecureGuid.Generate().ToString("N");
                    var resultFound = await this.dataContext.Sessions.CreateAsync(sessionId, refreshToken, actorId,
                        () =>
                        {
                            var jwtToken = GenerateToken(sessionId, actorId, claims
                                .Select(claim => new KeyValuePair<string, string>(claim.Type, claim.Value))
                                .ToDictionary());
                            return onSuccess(redirectUri, actorId, jwtToken, refreshToken);
                        },
                        () => alreadyExists());
                    return resultFound;
                },
                () => credentialNotInSystem().ToTask());
            return result;
        }

        public delegate T AuthenticateSuccessDelegate<T>(Guid authorizationId, string token, string refreshToken);
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
                async (authorizationId, claims) =>
                {
                    var updateAuthResult = await this.dataContext.Sessions.UpdateAuthentication<T>(sessionId,
                        async (authId, saveAuthId) =>
                        {
                            if (default(Guid) != authId)
                                return onAlreadyAuthenticated();

                            await saveAuthId(authorizationId);
                            var jwtToken = GenerateToken(sessionId, authorizationId, claims
                                .Select(claim => new KeyValuePair<string, string>(claim.Type, claim.Value))
                                .ToDictionary());
                            return onSuccess.Invoke(authorizationId, jwtToken, string.Empty);
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
            Func<Guid, System.Security.Claims.Claim[], T> onSuccess, 
            Func<string, T> onInvalidCredential,
            Func<T> onAuthIdNotFound,
            Func<string, T> systemUnavailable)
        {
            var provider = await this.context.GetCredentialProvider(method);
            return await provider.RedeemTokenAsync(token,
                (authorizationId, claims) =>
                {
                    return onSuccess(authorizationId, claims);
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

        private string GenerateToken(Guid sessionId, Guid authorizationId, Dictionary<string, string> claims)
        {
            var tokenExpirationInMinutesConfig = ConfigurationManager.AppSettings["BlackBarLabs.Security.SessionServer.tokenExpirationInMinutes"];
            if (string.IsNullOrEmpty(tokenExpirationInMinutesConfig))
                throw new SystemException("TokenExpirationInMinutes was not found in the configuration file");
            var tokenExpirationInMinutes = Double.Parse(tokenExpirationInMinutesConfig);
            
            var jwtToken = BlackBarLabs.Security.Tokens.JwtTools.CreateToken(
                sessionId, new Uri("http://example.com/Auth"),
                TimeSpan.FromMinutes(tokenExpirationInMinutes),
                claims,
                (token) => token,
                (configName) => configName,
                (configName, issue) => configName + ":" + issue,
                "BlackBarLabs.Security.SessionServer.issuer",
                "BlackBarLabs.Security.SessionServer.key");

            return jwtToken;
        }
        
    }
}

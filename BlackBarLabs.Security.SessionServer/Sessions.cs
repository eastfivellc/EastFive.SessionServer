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

        public delegate T CreateSessionSuccessDelegate<T>(Guid authorizationId, string token, string refreshToken);
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
                    return onSuccess.Invoke(default(Guid), jwtToken, refreshToken);
                },
                () => alreadyExists());
        }

        public async Task<T> CreateAsync<T>(Guid sessionId,
            CredentialValidationMethodTypes method, string token,
            CreateSessionSuccessDelegate<T> onSuccess,
            CreateSessionAlreadyExistsDelegate<T> alreadyExists,
            Func<string, T> invalidCredentials,
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
                                    return onSuccess(actorId, jwtToken, refreshToken);
                                },
                                () => alreadyExists());
                            return resultFound;
                        },
                        () => credentialNotInSystem().ToTask());
                },
                (why) => invalidCredentials(why).ToTask(),
                () => authIdNotFound().ToTask(),
                (why) => systemOffline(why).ToTask());
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
            var provider = this.context.GetCredentialProvider(method);
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

        //private IEnumerable<Claim> GetClaims(Guid sessionId, Guid authorizationId, SessionServer.Persistence.Claim [] claims)
        //{
        //    var claimsDefault = (IEnumerable<Claim>)new[] {
        //        new Claim(ClaimIds.Session, sessionId.ToString()),
        //        new Claim(ClaimIds.Authorization, authorizationId.ToString()) };

        //    var claimsExtra = claims.Select(
        //        (claim) => 
        //        {
        //            var typeString = claim.type == default(Uri) ? string.Empty : claim.type.AbsoluteUri;
        //            var issuerString = claim.issuer == default(Uri) ? string.Empty : claim.issuer.AbsoluteUri;
        //            return new Claim(typeString, claim.value, "string", issuerString);
        //        })
        //        .ToArray();

        //    return claimsDefault.Concat(claimsExtra);
        //}
    }
}

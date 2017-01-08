using System;
using System.Threading.Tasks;
using BlackBarLabs.Security.AuthorizationServer.Exceptions;
using BlackBarLabs.Security.Authorization;
using System.Configuration;
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using BlackBarLabs.Collections.Async;
using BlackBarLabs.Collections.Generic;

namespace BlackBarLabs.Security.AuthorizationServer
{
    public class Sessions
    {
        private Context context;
        private SessionServer.Persistence.IDataContext dataContext;

        internal Sessions(Context context, SessionServer.Persistence.IDataContext dataContext)
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
                var jwtToken = this.GenerateToken(sessionId, default(Guid), new Claim[] { });
                return onSuccess.Invoke(default(Guid), jwtToken, refreshToken);
                },
                () => alreadyExists());
            }

        public async Task<T> CreateAsync<T>(Guid sessionId,
            CredentialValidationMethodTypes method, Uri providerId, string username, string token,
            CreateSessionSuccessDelegate<T> onSuccess,
            CreateSessionAlreadyExistsDelegate<T> alreadyExists,
            Func<string, T> invalidCredentials)
        {
            var result = await await AuthenticateCredentialsAsync(method, providerId, username, token,
                async (authorizationId, claims) =>
                {
                    var refreshToken = BlackBarLabs.Security.SecureGuid.Generate().ToString("N");
                    var resultFound = await this.dataContext.Sessions.CreateAsync(sessionId, refreshToken, authorizationId,
                        () =>
                        {
                            var jwtToken = GenerateToken(sessionId, authorizationId, claims);
                            return onSuccess(authorizationId, jwtToken, refreshToken);
                        },
                        () => alreadyExists());
                    return resultFound;
                },
                (errorMessage) => Task.FromResult(invalidCredentials(errorMessage)),
                () => Task.FromResult(invalidCredentials("Credential failed")));
            return result;
        }
        
        public delegate T AuthenticateSuccessDelegate<T>(Guid authorizationId, string token, string refreshToken);
        public delegate T AuthenticateInvalidCredentialsDelegate<T>();
        public delegate T AuthenticateAlreadyAuthenticatedDelegate<T>();
        public delegate T AuthenticateNotFoundDelegate<T>(string message);
        public async Task<T> AuthenticateAsync<T>(Guid sessionId,
            CredentialValidationMethodTypes credentialValidationMethod, Uri credentialsProviderId, string username, string token,
            AuthenticateSuccessDelegate<T> onSuccess,
            AuthenticateInvalidCredentialsDelegate<T> onInvalidCredentials,
            AuthenticateAlreadyAuthenticatedDelegate<T> onAlreadyAuthenticated,
            AuthenticateNotFoundDelegate<T> onNotFound)
        {
            var result = await await AuthenticateCredentialsAsync(credentialValidationMethod, credentialsProviderId, username, token,
                async (authorizationId, claims) =>
                {
                    var updateAuthResult = await this.dataContext.Sessions.UpdateAuthentication<T>(sessionId,
                        async (authId, saveAuthId) =>
                        {
                            if (default(Guid) != authId)
                                return onAlreadyAuthenticated();

                            await saveAuthId(authorizationId);
                            var jwtToken = GenerateToken(sessionId, authorizationId, claims);
                            return onSuccess.Invoke(authorizationId, jwtToken, string.Empty);
                        },
                        () => onNotFound("Error updating authentication"));
                    return updateAuthResult;
                },
                (errorMessage) => Task.FromResult(onNotFound(errorMessage)),
                () => Task.FromResult(onInvalidCredentials()));
            return result;
        }

        private async Task<T> AuthenticateCredentialsAsync<T>(
            CredentialValidationMethodTypes method, Uri providerId, string username, string token,
            Func<Guid, SessionServer.Persistence.Claim[], T> onSuccess, Func<string, T> onAuthIdNotFound, Func<T> onInvalidCredential)
        {
            var provider = this.context.GetCredentialProvider(method);
            return await await provider.RedeemTokenAsync(providerId, username, token,
                async (accessToken) =>
                {
                    var result = await this.dataContext.Authorizations.FindAuthId(providerId, username,
                        (authorizationId, claims) =>
                        {
                            return onSuccess(authorizationId, claims);
                        },
                        () => onAuthIdNotFound("Could not find auth Id for username: " + username));
                    return result;
                },
                (errorMessage) => Task.FromResult(onAuthIdNotFound(errorMessage)),
                () => { throw new Exception("Could not connect to auth system"); });
        }

        private string GenerateToken(Guid sessionId, Guid authorizationId, SessionServer.Persistence.Claim [] claims)
        {
            var jwtClaims = GetClaims(sessionId, authorizationId, claims);
            return GenerateToken(sessionId, authorizationId, jwtClaims);
        }

        private string GenerateToken(Guid sessionId, Guid authorizationId, IEnumerable<Claim> claims)
        {
            var tokenExpirationInMinutesConfig = ConfigurationManager.AppSettings["BlackBarLabs.Security.AuthorizationServer.tokenExpirationInMinutes"];
            if (string.IsNullOrEmpty(tokenExpirationInMinutesConfig))
                throw new SystemException("TokenExpirationInMinutes was not found in the configuration file");
            var tokenExpirationInMinutes = Double.Parse(tokenExpirationInMinutesConfig);
            
            var jwtToken = Security.Tokens.JwtTools.CreateToken(
                sessionId, new Uri("http://example.com/Auth"),
                TimeSpan.FromMinutes(tokenExpirationInMinutes),
                claims.Select(claim => new KeyValuePair<string, string>(claim.Type, claim.Value)).ToDictionary(),
                (token) => token,
                (configName) => configName,
                (configName, issue) => configName + ":" + issue,
                "AuthServer.issuer", "AuthServer.key");

            return jwtToken;
        }

        private IEnumerable<Claim> GetClaims(Guid sessionId, Guid authorizationId, SessionServer.Persistence.Claim [] claims)
        {
            var claimsDefault = (IEnumerable<Claim>)new[] {
                new Claim(ClaimIds.Session, sessionId.ToString()),
                new Claim(ClaimIds.Authorization, authorizationId.ToString()) };

            var claimsExtra = claims.Select(
                (claim) => 
                {
                    var typeString = claim.type == default(Uri) ? string.Empty : claim.type.AbsoluteUri;
                    var issuerString = claim.issuer == default(Uri) ? string.Empty : claim.issuer.AbsoluteUri;
                    return new Claim(typeString, claim.value, "string", issuerString);
                })
                .ToArray();

            return claimsDefault.Concat(claimsExtra);
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.IdentityModel.Tokens;

using EastFive.Api.Services;
using System.Web;
using BlackBarLabs.Linq;
using System.IdentityModel.Tokens.Jwt;
using EastFive.Security.SessionServer.Persistence;
using BlackBarLabs.Extensions;
using EastFive.Security.SessionServer;

namespace EastFive.Security.CredentialProvider.AzureADB2C
{
    public class AzureADB2CProvider : IProvideLogin, IProvideLoginManagement
    {
        #region Setup

        internal const string StateKey = "state";
        internal const string IdTokenKey = "id_token";
        
        EastFive.AzureADB2C.B2CGraphClient client = new EastFive.AzureADB2C.B2CGraphClient();
        private TokenValidationParameters validationParameters;
        internal string audience;
        private Uri signinConfiguration;
        private Uri signupConfiguration;
        private string loginEndpoint;
        private string signupEndpoint;
        private string logoutEndpoint;

        public AzureADB2CProvider(string audience, Uri signinConfiguration, Uri signupConfiguration)
        {
            this.audience = audience;
            this.signinConfiguration = signinConfiguration;
            this.signupConfiguration = signupConfiguration;
        }

        public static TResult LoadFromConfig<TResult>(
            Func<AzureADB2CProvider, TResult> onLoaded,
            Func<string, TResult> onConfigurationNotAvailable)
        {
            return Web.Configuration.Settings.GetString(SessionServer.Configuration.AppSettings.AADB2CAudience,
                (audience) =>
                {
                    return Web.Configuration.Settings.GetUri(SessionServer.Configuration.AppSettings.AADB2CSigninConfiguration,
                        (signinConfiguration) =>
                        {
                            return Web.Configuration.Settings.GetUri(SessionServer.Configuration.AppSettings.AADB2CSignupConfiguration,
                                (signupConfiguration) =>
                                {
                                    var provider = new AzureADB2CProvider(audience, signinConfiguration, signupConfiguration);
                                    return onLoaded(provider);
                                },
                                onConfigurationNotAvailable);
                        },
                        onConfigurationNotAvailable);
                },
                onConfigurationNotAvailable);
        }

        public static async Task<TResult> InitializeAsync<TResult>(
            Func<IProvideAuthorization, TResult> onProvideAuthorization,
            Func<TResult> onProvideNothing,
            Func<string, TResult> onFailure)
        {
            return await LoadFromConfig(
                (provider) => EastFive.AzureADB2C.Libary.InitializeAsync(provider.signupConfiguration, provider.signinConfiguration, provider.audience,
                    (signupEndpoint, signinEndpoint, logoutEndpoint, validationParams) =>
                    {
                        provider.signupEndpoint = signupEndpoint;
                        provider.loginEndpoint = signinEndpoint;
                        provider.logoutEndpoint = logoutEndpoint;
                        provider.validationParameters = validationParams;
                        return onProvideAuthorization(provider);
                    },
                    (why) =>
                    {
                        return onFailure(why);
                    }),
                onProvideNothing.AsAsyncFunc<TResult, string>());
        }

        #endregion

        #region IProvideAuthorization

        public CredentialValidationMethodTypes Method => CredentialValidationMethodTypes.Password;

        public async Task<TResult> RedeemTokenAsync<TResult>(IDictionary<string, string> extraParams,
            Func<string, Guid?, Guid?, IDictionary<string, string>, TResult> onSuccess,
            Func<string, TResult> onInvalidCredentials,
            Func<string, TResult> onCouldNotConnect,
            Func<string, TResult> onUnspecifiedConfiguration,
            Func<string, TResult> onFailure)
        {
            if (!extraParams.ContainsKey(AzureADB2CProvider.IdTokenKey))
                return onFailure($"{AzureADB2CProvider.IdTokenKey} not in auth response");
            if (!extraParams.ContainsKey(AzureADB2CProvider.StateKey))
                return onFailure($"{AzureADB2CProvider.StateKey} not in auth response");

            var token = extraParams[AzureADB2CProvider.IdTokenKey];
            var stateParam = extraParams[AzureADB2CProvider.StateKey];
            return await this.ValidateToken(token,
                (claims) =>
                {
                    return Web.Configuration.Settings.GetString(
                            EastFive.Security.SessionServer.Configuration.AppSettings.LoginIdClaimType,
                        (claimType) =>
                        {
                            if (!Guid.TryParse(stateParam, out Guid stateId))
                                return onFailure($"Invalid state parameter [{stateParam}] is not a GUID");

                            var authClaims = claims.Claims
                                        .Where(claim => claim.Type.CompareTo(claimType) == 0)
                                        .ToArray();
                            if (authClaims.Length == 0)
                                return onFailure($"Token does not contain claim for [{claimType}] which is necessary to operate with this system");

                            string subject = authClaims[0].Value;
                            var authId = default(Guid?);
                            if (Guid.TryParse(subject, out Guid authIdGuid))
                                authId = authIdGuid;

                            // TODO: Populate extraParams from claims
                            return onSuccess(subject, stateId, authId, extraParams);
                        },
                        onUnspecifiedConfiguration);
                },
                onInvalidCredentials).ToTask();
        }

        private TResult ValidateToken<TResult>(string idToken,
            Func<ClaimsPrincipal, TResult> onSuccess,
            Func<string, TResult> onFailed)
        {
            if (default(TokenValidationParameters) == validationParameters)
                return onFailed("AADB2C Provider not initialized");
            var handler = new JwtSecurityTokenHandler();
            try
            {
                var claims = handler.ValidateToken(idToken, validationParameters, out SecurityToken validatedToken);
                //var claims = new ClaimsPrincipal();
                return onSuccess(claims);
            }
            catch (Microsoft.IdentityModel.Tokens.SecurityTokenException ex)
            {
                return onFailed(ex.Message);
            }
        }
        
        #endregion

        #region IProvideLogin

        public Uri GetLoginUrl(Guid state, Uri responseLocation)
        {
            return GetUrl(this.loginEndpoint, state, responseLocation);
        }

        public Uri GetLogoutUrl(Guid state, Uri responseLocation)
        {
            return GetUrl(this.logoutEndpoint, state, responseLocation);
        }

        public Uri GetSignupUrl(Guid state, Uri callbackLocation)
        {
            return GetUrl(this.signupEndpoint, state, callbackLocation);
        }
        
        private Uri GetUrl(string longurl, Guid stateGuid,
            Uri callbackLocation)
        {
            var uriBuilder = new UriBuilder(longurl);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["client_id"] = this.audience;
            query["response_type"] = AzureADB2CProvider.IdTokenKey;
            query["redirect_uri"] = callbackLocation.AbsoluteUri;
            query["response_mode"] = "form_post";
            query["scope"] = "openid";
            
            query[StateKey] = stateGuid.ToString("N"); //  redirect_uri.Base64(System.Text.Encoding.ASCII);

            query["nonce"] = Guid.NewGuid().ToString("N");
            // query["p"] = "B2C_1_signin1";
            uriBuilder.Query = query.ToString();
            var redirect = uriBuilder.Uri; // .ToString();
            return redirect;
        }
        
        #endregion

        #region IProvideLoginManagement

        public async Task<TResult> CreateAuthorizationAsync<TResult>(string displayName, 
                string userId, bool isEmail, string secret, bool forceChange,
            Func<Guid, TResult> onSuccess,
            Func<Guid, TResult> usernameAlreadyInUse,
            Func<TResult> onPasswordInsufficent,
            Func<string, TResult> onServiceNotAvailable,
            Func<TResult> onServiceNotSupported, 
            Func<string, TResult> onFailure)
        {
            return await client.CreateUser(displayName,
                userId, isEmail, secret, forceChange,
                onSuccess,
                (loginId) => usernameAlreadyInUse(loginId),
                onPasswordInsufficent,
                onFailure);
        }
        
        public async Task<TResult> GetAuthorizationAsync<TResult>(Guid loginId, 
            Func<LoginInfo, TResult> onSuccess,
            Func<TResult> onNotFound,
            Func<string, TResult> onServiceNotAvailable, 
            Func<TResult> onServiceNotSupported,
            Func<string, TResult> onFailure)
        {
            return await client.GetUserByObjectId(loginId.ToString(),
                (displayName, signinName, isEmail, forceChange, accountEnabled) => onSuccess(new LoginInfo
                {
                    loginId = loginId,
                    accountEnabled = accountEnabled,
                    forceChange = forceChange,
                    isEmail = isEmail,
                    userName = signinName,
                    displayName = displayName,
                    forceChangePassword = forceChange,
                }),
                (why) => onServiceNotAvailable(why));
        }

        public async Task<TResult> GetAllAuthorizationsAsync<TResult>(
            Func<LoginInfo[], TResult> onFound,
            Func<string, TResult> onServiceNotAvailable,
            Func<TResult> onServiceNotSupported, 
            Func<string, TResult> onFailure)
        {
            var total = new LoginInfo[] { };
            return await client.GetAllUsersAsync(
                tuples =>
                {
                    total = total.Concat(tuples
                        .Select(tuple =>
                            new LoginInfo
                            {
                                loginId = tuple.Item1,
                                userName = tuple.Item2,
                                isEmail = tuple.Item3,
                                forceChange = tuple.Item4,
                                accountEnabled = tuple.Item5,
                            }))
                        .ToArray();
                },
                () => onFound(total),
                (why) => onFailure(why));
        }

        public async Task<TResult> UpdateAuthorizationAsync<TResult>(Guid loginId,
                string password, bool forceChange,
            Func<TResult> onSuccess,
            Func<string, TResult> onServiceNotAvailable,
            Func<TResult> onServiceNotSupported, 
            Func<string, TResult> onFailure)
        {
            var result = await client.UpdateUserPasswordAsync(loginId.ToString(), password, forceChange);
            return onSuccess();
        }

        public async Task<TResult> DeleteAuthorizationAsync<TResult>(Guid loginId,
            Func<TResult> onSuccess,
            Func<string, TResult> onServiceNotAvailable, 
            Func<TResult> onServiceNotSupported,
            Func<string, TResult> onFailure)
        {
            var result = await client.DeleteUser(loginId.ToString());
            return onSuccess();
        }
        
        #endregion
        
    }
}

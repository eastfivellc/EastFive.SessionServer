using BlackBarLabs;
using BlackBarLabs.Api.Extensions;
using BlackBarLabs.Extensions;
using BlackBarLabs.Linq;
using EastFive.Collections.Generic;
using EastFive.Security.SessionServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Security.CredentialProvider
{
    public class LightspeedProvider : IProvideLogin
    {
        #region Initialization

        private const string tokenKey = "code";
        private const string stateKey = "state";
        public const string accessTokenKey = "access_token";
        public const string refreshTokenKey = "refresh_token";
        public const string accountIdKey = "account_id";
        public const string customerIdKey = "systemCustomerID";
        public const string apiClientIdKey = "systemAPIClientID";
        public const string apiKeyId = "systemAPIKeyID";

        private readonly string clientId;
        private readonly string clientSecret;

        private LightspeedProvider(string clientKey, string clientSecret)
        {
            this.clientId = clientKey;
            this.clientSecret = clientSecret;
        }
        
        public static TResult LoadFromConfig<TResult>(
            Func<LightspeedProvider, TResult> onLoaded,
            Func<string, TResult> onConfigurationNotAvailable)
        {
            return Web.Configuration.Settings.GetString(SessionServer.Configuration.AppSettings.OAuth.Lightspeed.ClientKey,
                (clientKey) =>
                {
                    return Web.Configuration.Settings.GetString(SessionServer.Configuration.AppSettings.OAuth.Lightspeed.ClientSecret,
                        (clientSecret) =>
                        {
                            var provider = new LightspeedProvider(clientKey, clientSecret);
                            return onLoaded(provider);
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
                (provider) => onProvideAuthorization(provider),
                (why) => onFailure(why)).ToTask();
        }

        #endregion

        #region IProvideAuthorization

        public CredentialValidationMethodTypes Method => CredentialValidationMethodTypes.Lightspeed;

        public async Task<TResult> GetRefreshTokenAsync<TResult>(string code, string grantType,
            Func<string, string, string, string, string, TResult> onSuccess,
            Func<string, TResult> onInvalidCredentials)
        {
            // Parameter Description
            // client_id Your application’s client ID specified when you registered your client.
            // client_secret The client secret you specified when you registered your client.
            // code The code you received after the user authorized your application.
            // grant_type Specify ‘authorization_code’ to get the access token.

            var clientIdContent = new StringContent(this.clientId);
            var clientSecretContent = new StringContent(this.clientSecret);
            var codeContent = new StringContent(code);
            var refreshTokenContent = new StringContent(code);
            var grantTypeContent = new StringContent(grantType);

            // Submit the form using HttpClient and 
            // create form data as Multipart (enctype="multipart/form-data")

            using (var client = new HttpClient())
            using (var formData = new MultipartFormDataContent())
            {
                formData.Add(clientIdContent, "client_id");
                formData.Add(clientSecretContent, "client_secret");
                formData.Add(codeContent, "code");
                formData.Add(refreshTokenContent, "refresh_token");
                formData.Add(grantTypeContent, "grant_type");

                // Invoke the request to the server

                var url = "https://cloud.merchantos.com/oauth/access_token.php";
                var response = await client.PostAsync(url, formData);

                // ensure the request was a success
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return onInvalidCredentials($"Token validation failed:{error}");
                }
                
                //{"access_token":"ede8a1eca4f1077a06d84f662b112dab5ac8be86","expires_in":1800,"token_type":"bearer","scope":"employee:register_read employee:inventory employee:admin_inventory systemuserid:491223","refresh_token":"3f82439dc8355081a462be15f68d218fdf81b2ca"}
                var content = await response.Content.ReadAsStringAsync();
                dynamic stuff = Newtonsoft.Json.JsonConvert.DeserializeObject(content);

                // access_token The access token that has been granted.
                var accessToken = (string)stuff.access_token;
                // expires_in The lifetime in seconds of this access token. 3600 means that the token will expire in 60 minutes.
                var expiresIn = (string)stuff.expires_in;
                // In some cases an existing token will be returned instead of a new token.This field will indicate how many seconds are left until it expires.
                // token_type The type of token granted. This will always be “bearer”.
                var tokenType = (string)stuff.token_type;
                // scope The access scope(s) granted and the systemUserID(used internally by Lightspeed Retail and unique across all accounts) of the user that authorized your application.
                var scope = (string)stuff.scope;
                // refresh_token The refresh token that can be used when this access token expires to get new one.
                var refreshToken = (string)stuff.refresh_token;

                return onSuccess(accessToken, expiresIn, tokenType, scope, refreshToken);
            }
        }

        public async Task<TResult> RedeemTokenAsync<TResult>(IDictionary<string, string> tokenParameters, 
            Func<string, Guid?, Guid?, IDictionary<string, string>, TResult> onSuccess,
            Func<Guid?, IDictionary<string, string>, TResult> onUnauthenticated,
            Func<string, TResult> onInvalidCredentials,
            Func<string, TResult> onCouldNotConnect, 
            Func<string, TResult> onUnspecifiedConfiguration, 
            Func<string, TResult> onFailure)
        {
            if (!tokenParameters.ContainsKey(LightspeedProvider.tokenKey))
                return onInvalidCredentials($"Parameter with name [{LightspeedProvider.tokenKey}] was not provided");

            var code = tokenParameters[LightspeedProvider.tokenKey];

            var state = default(Guid?);
            if (tokenParameters.ContainsKey(LightspeedProvider.stateKey))
                if (Guid.TryParse(tokenParameters[LightspeedProvider.stateKey], out Guid parsedState))
                    state = parsedState;

            // Parameter Description
            // client_id Your application’s client ID specified when you registered your client.
            // client_secret The client secret you specified when you registered your client.
            // code The code you received after the user authorized your application.
            // grant_type Specify ‘authorization_code’ to get the access token.

            var clientIdContent = new StringContent(this.clientId);
            var clientSecretContent = new StringContent(this.clientSecret);
            var codeContent = new StringContent(code);
            var grantTypeContent = new StringContent("authorization_code");

            // Submit the form using HttpClient and 
            // create form data as Multipart (enctype="multipart/form-data")

            return await await GetRefreshTokenAsync(code, "authorization_code",
                    async (accessToken, expiresIn, tokenType, scope, refreshToken) =>
                    {
                        //var match = System.Text.RegularExpressions.Regex.Match(scope, ".*systemuserid\\:\\([0-9]+\\)");
                        //var subject = (match.Success && match.Groups.Count > 1) ?
                        //    match.Groups[2].Value
                        //    :
                        //    await GetSessionUserIdAsync(client, accessToken);

                        return await GetSessionUserIdAsync(accessToken,
                            (customerId, subject, apiClientId, apiKeyId) =>
                            {
                                if (subject.IsNullOrWhiteSpace())
                                    return onInvalidCredentials("systemuserid not provided in scope list");

                                var extraParams = new Dictionary<string, string>
                                {
                                    {  LightspeedProvider.accessTokenKey, accessToken },
                                    {  "expires_in", expiresIn },
                                    {  "token_type", tokenType },
                                    {  LightspeedProvider.refreshTokenKey, refreshToken },
                                    {  "scope", scope },
                                    {  LightspeedProvider.accountIdKey, subject },
                                    {  LightspeedProvider.customerIdKey, customerId },
                                    {  LightspeedProvider.apiClientIdKey, apiClientId },
                                    {  LightspeedProvider.apiKeyId, apiKeyId },
                                };
                                return onSuccess(subject, state, default(Guid?), extraParams);
                            });
                    },
                    onInvalidCredentials.AsAsyncFunc());
        }

        private async Task<TResult> GetSessionUserIdAsync<TResult>(string accessToken,
            Func<string, string, string, string, TResult> onSuccess)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    var result = await client.GetAsync("https://api.merchantos.com/API/Session.json");
                    // {"sessionID":"","sessionCookie":"PHPSESSID=; path=\/","employeeID":"1",
                    // "systemCustomerID":"159721","systemUserID":"491223","systemAPIClientID":"59386","systemAPIKeyID":"110279787",
                    // "shopCount":"1","Employee":{"employeeID":"1","firstName":"Josh","lastName":"Wingstrom","singleShop":"false","hasPin":"false","isCheckedIn":"false","checkedInAt":""},
                    // "Rights":{"register_read":"true","categories":"true","inventory_base":"true","inventory_counts":"true","inventory_import":"true","inventory_read":"true","manufacturers":"true","product_cost":"true","product_edit":"true","purchase_orders":"true","special_orders":"true","tags":"true","transfers":"true","vendor_returns":"true","vendors":"true","vr_reasons":"true","admin_inventory":"true"}}
                    var sessionContent = await result.Content.ReadAsStringAsync();
                    dynamic stuff = Newtonsoft.Json.JsonConvert.DeserializeObject(sessionContent);
                    var systemCustomerID = (string)stuff.systemCustomerID;
                    var systemUserID = (string)stuff.systemUserID;
                    var systemAPIClientID = (string)stuff.systemAPIClientID;
                    var systemAPIKeyID = (string)stuff.systemAPIKeyID;
                    return onSuccess(systemCustomerID, systemUserID, systemAPIClientID, systemAPIKeyID);
                }
            } catch(Exception ex)
            {
                ex.GetType();
                throw;
            }
        }

        #endregion

        #region IProvideLogin

        public Type CallbackController => typeof(SessionServer.Api.Controllers.OAuthResponseLightspeedController);

        public Uri GetLoginUrl(Guid state, Uri responseControllerLocation)
        {
            // response_type -- Ask for ‘code’ which will give you a temporary token that you can then use to get an access token.
            // https://cloud.merchantos.com/oauth/authorize.php?response_type=code&client_id={client_id}&scope={scope}&state={state}
            var loginScopes = "employee:all+employee:register_read+employee:inventory+employee:admin_inventory";
            var stateString = state.ToString("N");
            var url = $"https://cloud.merchantos.com/oauth/authorize.php?response_type=code&client_id={this.clientId}&scope={loginScopes}&state={stateString}";
            return new Uri(url);
        }

        public Uri GetLogoutUrl(Guid state, Uri responseControllerLocation)
        {
            return responseControllerLocation
                .AddQuery(LightspeedProvider.stateKey, state.ToString("N"))
                .AddQuery("method", CredentialValidationMethodTypes.Lightspeed.ToString());
        }

        public Uri GetSignupUrl(Guid state, Uri responseControllerLocation)
        {
            return default(Uri);
        }
        
        #endregion

        
    }
}

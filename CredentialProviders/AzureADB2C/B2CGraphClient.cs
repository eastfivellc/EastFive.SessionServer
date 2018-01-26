using BlackBarLabs.Extensions;
using BlackBarLabs.Linq;
using EastFive.Linq;
using Microsoft.Azure;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.AzureADB2C
{
    public class B2CGraphClient
    {
        private string clientId { get; set; }
        private string clientSecret { get; set; }
        private string tenant { get; set; }

        private AuthenticationContext authContext;
        private ClientCredential credential;
        
        private B2CGraphClient(string clientId, string clientSecret, string tenant)
        {
            // The client_id, client_secret, and tenant are pulled in from the App.config file
            this.clientId = clientId;
            this.clientSecret = clientSecret;
            this.tenant = tenant;

            // The AuthenticationContext is ADAL's primary class, in which you indicate the direcotry to use.
            this.authContext = new AuthenticationContext("https://login.microsoftonline.com/" + tenant);

            // The ClientCredential is where you pass in your client_id and client_secret, which are 
            // provided to Azure AD in order to receive an access_token using the app's identity.
            this.credential = new ClientCredential(clientId,  clientSecret);
        }

        internal static TResult LoadFromConfig<TResult>(
            Func<B2CGraphClient, TResult> onSuccess,
            Func<string, TResult> onConfigurationNotAvailable)
        {
            // The client_id, client_secret, and tenant are pulled in from the App.config file
            return Web.Configuration.Settings.GetString(AppSettings.ClientId,
                   (clientId) =>
                   {
                       return Web.Configuration.Settings.GetString(AppSettings.ClientSecret,
                           (signinConfiguration) =>
                           {
                               return Web.Configuration.Settings.GetString(AppSettings.Tenant,
                                   (signupConfiguration) =>
                                   {
                                       try
                                       {
                                           var client = new B2CGraphClient(clientId, signinConfiguration, signupConfiguration);
                                           return onSuccess(client);
                                       } catch(Exception ex)
                                       {
                                           return onConfigurationNotAvailable(ex.Message);
                                       }
                                   },
                                   onConfigurationNotAvailable);
                           },
                           onConfigurationNotAvailable);
                   },
                   onConfigurationNotAvailable);
        }

        public async Task<TResult> GetUserByObjectId<TResult>(string objectId,
            Func<string, string, bool, bool, bool, TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            try
            {
                var userResult = await SendGraphGetRequest("/users/" + objectId, null);
                var user = JsonConvert.DeserializeObject<Resources.User>(userResult);
                var signinName = default(string);
                var displayName = default(string);
                var isEmail = default(bool);
                if (default(Resources.User.SignInName[]) != user.SignInNames &&
                    user.SignInNames.Length > 0)
                {
                    displayName = user.DisplayName;
                    signinName = user.SignInNames[0].Value;
                    isEmail = String.Compare(user.SignInNames[0].Type, "emailAddress") == 0;
                }
                var forceChange = default(Resources.User.PasswordProfileResource) != user.PasswordProfile
                    ? user.PasswordProfile.ForceChangePasswordNextLogin
                    : default(bool);

                return onSuccess(displayName, signinName, isEmail, forceChange, user.AccountEnabled);
            }
            catch (Exception e)
            {
                return onFailure(e.Message);
            }
        }

        public async Task<TResult> GetUserByUserIdAsync<TResult>(string userId,
            Func<Guid, bool, bool, bool, TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            var userResult = await SendGraphGetRequest("/users/", 
                $"$filter=signInNames/any(x:x/value eq '{userId}')");
            var userData = JsonConvert.DeserializeObject<Resources.ODataMetadata<Resources.User[]>>(userResult);
            if (default(Resources.ODataMetadata<Resources.User[]>) == userData)
                return onFailure("Could not parse result data");
            var users = userData.Value.NullToEmpty().ToArray();
            if (users.Length < 1)
                return onFailure("User not found");
            var user = ParseUser(users[0]);
            if (null == user)
                return onFailure("Could not parse user");
            return onSuccess(user.Item1, user.Item3, user.Item4, user.Item5);
        }

        private Tuple<Guid, string, bool, bool, bool> ParseUser(Resources.User user)
        {
            Guid loginId;
            if (!Guid.TryParse(user.ObjectId, out loginId))
                return default(Tuple<Guid, string, bool, bool, bool>);
            var isEmail = false;
            var userName = string.Empty;
            if (default(Resources.User.SignInName[]) != user.SignInNames &&
                user.SignInNames.Length > 0)
            {
                isEmail = String.Compare(user.SignInNames[0].Type, "emailAddress") == 0;
                userName = user.SignInNames[0].Value;
            }
            var forceChange = default(Resources.User.PasswordProfileResource) != user.PasswordProfile && user.PasswordProfile.ForceChangePasswordNextLogin;
            return new Tuple<Guid, string, bool, bool, bool>(loginId,userName,isEmail,forceChange,user.AccountEnabled);
        }

        public async Task<TResult> GetAllUsersAsync<TResult>(
            Action<Tuple<Guid,string,bool,bool,bool>[]> onSegment,
            Func<TResult> onSuccess,
            Func<string,TResult> onFailure)
        {
            const string skipToken = "$skiptoken=X'";
            var query = string.Empty;
            do
            {
                var userResponse = await SendGraphGetRequest("/users", query);
                var userPayload = JsonConvert.DeserializeObject<Resources.ODataMetadata<Resources.User[]>>(userResponse);
                if (null == userPayload)
                    return onFailure("Could not parse result data");

                var userValues = userPayload.Value.NullToEmpty().ToArray();
                if (userValues.Length == 0)
                    break;

                onSegment(userValues
                    .Select(ParseUser)
                    .SelectWhereNotNull()
                    .ToArray());

                var index = userResponse.LastIndexOf(skipToken);
                if (index == -1)
                    break;
                query = userResponse.Substring(index, userResponse.IndexOf("'", index + skipToken.Length) + 1 - index);
            } while (true);
            return onSuccess();
        }

        public async Task<string> GetAllUsers(string query)
        {
            return await SendGraphGetRequest("/users", query);
        }

        public async Task<TResult> CreateUser<TResult>(string displayName,
            string userId, bool isEmail, string secret, bool forceChange,
            Func<Guid, TResult> onSuccess,
            Func<Guid, TResult> onUserIdAlreadyExists,
            Func<TResult> onPasswordInsufficent,
            Func<string, TResult> onFail)
        {
            var user = new EastFive.AzureADB2C.Resources.User()
            {
                DisplayName = displayName,
                AccountEnabled = true,
                SignInNames = new[] {
                    new EastFive.AzureADB2C.Resources.User.SignInName
                    {
                        Type = isEmail? "emailAddress" : "userName",
                        Value = userId,
                    }
                },
                PasswordProfile = new EastFive.AzureADB2C.Resources.User.PasswordProfileResource
                {
                    ForceChangePasswordNextLogin = forceChange,
                    Password = secret,
                },
            };
            var json = JsonConvert.SerializeObject(user);
            var result = await await SendGraphPostRequest("/users", json,
                (resultJson) =>
                {
                    var resultUser = JsonConvert.DeserializeObject<Resources.User>(resultJson);
                    Guid objectId;
                    if (Guid.TryParse(resultUser.ObjectId, out objectId))
                        return onSuccess(objectId).ToTask();
                    return onFail("Could not parse ID in response").ToTask();
                },
                async (code, reason, issue) =>
                {
                    if (
                        (String.Compare("Request_BadRequest", issue.OdataError.Code) == 0)
                        &&
                        issue.OdataError.Values.NullToEmpty().Any(errorValue =>
                            (String.Compare("PropertyName", errorValue.Item) == 0 &&
                             String.Compare("signInNames", errorValue.Value) == 0)))
                    {
                        return await GetUserByUserIdAsync(userId,
                            (loginId, isEmailCurrent, forceChangeCurrent, accountEnabled) => onUserIdAlreadyExists(loginId),
                            (why) => onUserIdAlreadyExists(default(Guid)));
                    }
                    if (
                        (String.Compare("Request_BadRequest", issue.OdataError.Code) == 0)
                        &&
                        default(Resources.ODataError.Error.MessageType) != issue.OdataError.Message
                        &&
                        !String.IsNullOrWhiteSpace(issue.OdataError.Message.Value)
                        &&
                        issue.OdataError.Message.Value.Contains("The specified password does not comply with password complexity requirements"))
                    {
                        return onPasswordInsufficent();
                    }
                    return onFail($"{reason} : {issue.OdataError.Message.Value}");
                });
            return result;
        }

        public async Task<TResult> UpdateUserPasswordAsync<TResult>(string objectId, string password, bool forceChange,
            Func<string,TResult> onSuccess,
            Func<string,TResult> onFailure)
        {
            var user = new EastFive.AzureADB2C.Resources.UserPasswordPatch()
            {
                ObjectId = objectId,
                PasswordProfile = new EastFive.AzureADB2C.Resources.User.PasswordProfileResource
                {
                    ForceChangePasswordNextLogin = forceChange,
                    Password = password,
                },
            };
            var json = JsonConvert.SerializeObject(user);
            return await SendGraphPatchRequest("/users/" + objectId, json,
                onSuccess, onFailure);
        }

        public async Task<string> DeleteUser(string objectId)
        {
            return await SendGraphDeleteRequest("/users/" + objectId);
        }

        public async Task<TResult> RegisterExtension<TResult>(string objectId, string body,
            Func<TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            return await SendGraphPostRequest("/applications/" + objectId + "/extensionProperties", body,
                (responseBody) => onSuccess(),
                (code, why, error) => onFailure(why));
        }

        public async Task<string> UnregisterExtension(string appObjectId, string extensionObjectId)
        {
            return await SendGraphDeleteRequest("/applications/" + appObjectId + "/extensionProperties/" + extensionObjectId);
        }

        public async Task<string> GetExtensions(string appObjectId)
        {
            return await SendGraphGetRequest("/applications/" + appObjectId + "/extensionProperties", null);
        }

        public async Task<string> GetApplications(string query)
        {
            return await SendGraphGetRequest("/applications", query);
        }

        private async Task<string> SendGraphDeleteRequest(string api)
        {
            // NOTE: This client uses ADAL v2, not ADAL v4
            AuthenticationResult result = await authContext.AcquireTokenAsync(Globals.aadGraphResourceId, credential);
            HttpClient http = new HttpClient();
            string url = Globals.aadGraphEndpoint + tenant + api + "?" + Globals.aadGraphVersion;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            HttpResponseMessage response = await http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                object formatted = JsonConvert.DeserializeObject(error);
                throw new WebException("Error Calling the Graph API: \n" + JsonConvert.SerializeObject(formatted, Formatting.Indented));
            }

            return await response.Content.ReadAsStringAsync();
        }

        private async Task<TResult> SendGraphPatchRequest<TResult>(string api, string json,
            Func<string,TResult> onSuccess,
            Func<string,TResult> onFailure)
        {
            // NOTE: This client uses ADAL v2, not ADAL v4
            AuthenticationResult result = await authContext.AcquireTokenAsync(Globals.aadGraphResourceId, credential);
            HttpClient http = new HttpClient();
            string url = Globals.aadGraphEndpoint + tenant + api + "?" + Globals.aadGraphVersion;

            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                object formatted = JsonConvert.DeserializeObject(error);
                return onFailure(error);
            }

            return onSuccess(await response.Content.ReadAsStringAsync());
        }

        private async Task<TResult> SendGraphPostRequest<TResult>(string api, string json,
            Func<string, TResult> onSuccess,
            Func<HttpStatusCode, string, Resources.ODataError, TResult> onFailed)
        {
            AuthenticationResult result = await authContext.AcquireTokenAsync(Globals.aadGraphResourceId, credential);
            HttpClient http = new HttpClient();
            string url = Globals.aadGraphEndpoint + tenant + api + "?" + Globals.aadGraphVersion;
            
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                var issue = JsonConvert.DeserializeObject<Resources.ODataError>(error);
                return onFailed(response.StatusCode, response.ReasonPhrase, issue);
            }
            
            var contents = await response.Content.ReadAsStringAsync();
            return onSuccess(contents);
        }

        public async Task<string> SendGraphGetRequest(string api, string query)
        {
            // First, use ADAL to acquire a token using the app's identity (the credential)
            // The first parameter is the resource we want an access_token for; in this case, the Graph API.
            AuthenticationResult result = null;
            try
            {
                result = await authContext.AcquireTokenAsync("https://graph.windows.net", credential);
            } catch(Exception ex)
            {
                ex.GetType();
            }

            // For B2C user managment, be sure to use the 1.6 Graph API version.
            HttpClient http = new HttpClient();
            string url = "https://graph.windows.net/" + tenant + api + "?" + "api-version=1.6";
            if (!string.IsNullOrEmpty(query))
            {
                url += "&" + query;
            }

            // Append the access token for the Graph API to the Authorization header of the request, using the Bearer scheme.
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            HttpResponseMessage response = await http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                object formatted = JsonConvert.DeserializeObject(error);
                throw new WebException("Error Calling the Graph API: \n" + JsonConvert.SerializeObject(formatted, Formatting.Indented));
            }

            return await response.Content.ReadAsStringAsync();
        }
    }
}

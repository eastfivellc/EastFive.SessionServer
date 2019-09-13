using EastFive.Security.CredentialProvider;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using EastFive.Security.SessionServer.Persistence;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using EastFive.Api.Services;
using System.Security.Claims;
using EastFive.Security.SessionServer;
using System.Collections;
using EastFive.Serialization;
using EastFive.Web.Configuration;
using EastFive.Extensions;
using EastFive.Api.Controllers;
using EastFive.Linq;
using BlackBarLabs.Extensions;
using EastFive.Collections.Generic;
using BlackBarLabs.Api;

namespace EastFive.Api.Azure.Credentials
{
    [Attributes.IntegrationName(PinterestProvider.IntegrationName)]
    public class PinterestProvider : IProvideLogin
    {
        public const string IntegrationName = "Pinterest";
        public string Method => IntegrationName;
        public Guid Id => System.Text.Encoding.UTF8.GetBytes(Method).MD5HashGuid();

        public const string AuthorizationCode = "code";
        public const string Subject = "pinterest.subject";
        public const string refreshTokenKey = "refresh";
        public const string State = "state";

        public PinterestProvider()
        {
        }
        
        [Attributes.IntegrationName(PinterestProvider.IntegrationName)]
        public static Task<TResult> InitializeAsync<TResult>(
            Func<IProvideAuthorization, TResult> onProvideAuthorization,
            Func<TResult> onProvideNothing,
            Func<string, TResult> onFailure)
        {
            return onProvideAuthorization(new PinterestProvider()).AsTask();
        }
        
        public Type CallbackController => typeof(PinterestRedirect);

        public virtual async Task<TResult> RedeemTokenAsync<TResult>(IDictionary<string, string> extraParams,
            Func<string, Guid?, Guid?, IDictionary<string, string>, TResult> onSuccess,
            Func<Guid?, IDictionary<string, string>, TResult> onUnauthenticated,
            Func<string, TResult> onInvalidCredentials,
            Func<string, TResult> onCouldNotConnect,
            Func<string, TResult> onUnspecifiedConfiguration,
            Func<string, TResult> onFailure)
        {
            if (!extraParams.ContainsKey(PinterestProvider.AuthorizationCode))
                return onInvalidCredentials("grant_type/authorization code was not provided");
            var authorizationCode = extraParams[PinterestProvider.AuthorizationCode];

            return await EastFive.Azure.AppSettings.Pinterest.AppKey.ConfigurationString(
                (clientId) =>
                {
                    return EastFive.Azure.AppSettings.Pinterest.AppSecret.ConfigurationString(
                        async (clientSecret) =>
                        {
                            using (var httpClient = new HttpClient())
                            {
                                var tokenUrl = new Uri("https://api.pinterest.com/v1/oauth/token")
                                    .AddQueryParameter("grant_type", "authorization_code")
                                    .AddQueryParameter("client_id", clientId)
                                    .AddQueryParameter("client_secret", clientSecret)
                                    .AddQueryParameter("code", authorizationCode);

                                var request = new HttpRequestMessage(
                                    new HttpMethod("POST"), tokenUrl);
                                try
                                {
                                    var response = await httpClient.SendAsync(request);
                                    var content = await response.Content.ReadAsStringAsync();
                                    if (!response.IsSuccessStatusCode)
                                        return onFailure(content);

                                    dynamic stuff = null;
                                    try
                                    {
                                        stuff = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(content);
                                    }
                                    catch (Newtonsoft.Json.JsonReaderException)
                                    {
                                        return onCouldNotConnect($"Pinterest returned non-json response:{content}");
                                    }
                                    var extraParamsWithTokenValues = new Dictionary<string, string>(extraParams);
                                    foreach (var item in stuff)
                                    {
                                        extraParamsWithTokenValues.Add(item.Key.ToString(), item.Value.ToString());
                                    }

                                    string accessToken = (string)stuff["access_token"];
                                    var tokenType = (string)stuff["token_type"];
                                    var userRequest = new HttpRequestMessage(
                                        new HttpMethod("GET"),
                                        new Uri($"https://api.pinterest.com/v1/me?access_token={accessToken}"));
                                    //userRequest.Headers.Authorization = new AuthenticationHeaderValue(tokenType, accessToken);
                                    var userResponse = await httpClient.SendAsync(userRequest);
                                    var userContent = await userResponse.Content.ReadAsStringAsync();
                                    if (!userResponse.IsSuccessStatusCode)
                                        return onFailure(userContent);
                                    dynamic userStuff = null;
                                    try
                                    {
                                        userStuff = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(userContent);
                                    }
                                    catch (Newtonsoft.Json.JsonReaderException)
                                    {
                                        return onCouldNotConnect($"Pinterest returned non-json response:{userContent}");
                                    }
                                    foreach (var items in userStuff)
                                    {
                                        foreach (var item in items)
                                        {
                                            extraParamsWithTokenValues.Add(item.Key.ToString(), item.Value.ToString());
                                        }
                                    }
                                    var subject = (string)((dynamic)userStuff)["data"].id;
                                    return onSuccess(subject, default(Guid?), Guid.NewGuid(), extraParamsWithTokenValues);
                                }
                                catch (System.Net.Http.HttpRequestException ex)
                                {
                                    return onCouldNotConnect($"{ex.GetType().FullName}:{ex.Message}");
                                }
                                catch (Exception exGeneral)
                                {
                                    return onCouldNotConnect(exGeneral.Message);
                                }
                            }
                        },
                        (why) => onUnspecifiedConfiguration(why).AsTask());
                },
                (why) => onUnspecifiedConfiguration(why).AsTask());
        }

        public static object Initialize<T>(
                IDictionary<string, string> parameters, 
            Func<IDictionary<string, string>, Task> refreshTokensAsync,
            Func<object, T> onInitialized, 
            Func<string, T> onInvalid)
        {
            throw new NotImplementedException();
        }

        public TResult ParseCredentailParameters<TResult>(IDictionary<string, string> responseParams, 
            Func<string, Guid?, Guid?, TResult> onSuccess, 
            Func<string, TResult> onFailure)
        {
            if (!responseParams.ContainsKey(Subject))
                return onFailure("Missing pingone.subject");

            string subject = responseParams[Subject];
            var hash = SHA512.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(subject));
            var loginId = new Guid(hash.Take(16).ToArray());

            return onSuccess(subject, default(Guid?), loginId);
        }

        #region IProvideLogin

        public Uri GetLogoutUrl(Guid state, Uri responseControllerLocation, Func<Type, Uri> controllerToLocation)
        {
            return default(Uri);
        }

        public Uri GetLoginUrl(Guid state, Uri responseControllerLocation, Func<Type, Uri> controllerToLocation)
        {
            return EastFive.Azure.AppSettings.Pinterest.AppKey.ConfigurationString(
                applicationId =>
                {
                    return new Uri("https://api.pinterest.com/oauth/")
                        .AddQueryParameter("response_type", "code")
                        .AddQueryParameter("client_id", applicationId)
                        .AddQueryParameter("scope", "read_public")
                        .AddQueryParameter("state", state.ToString("N"))
                        .AddQueryParameter("redirect_uri", responseControllerLocation.AbsoluteUri);
                });
        }

        public Uri GetSignupUrl(Guid state, Uri responseControllerLocation, Func<Type, Uri> controllerToLocation)
        {
            return default(Uri);
        }

        #endregion
    }

    [FunctionViewController6(
        Route = "PinterestRedirect",
        Resource = typeof(PinterestRedirect),
        ContentType = "x-application/pinterest-redirect",
        ContentTypeVersion = "0.1")]
    public class PinterestRedirect : EastFive.Azure.Auth.Redirection
    {
        public const string StatePropertyName = PinterestProvider.State;
        [ApiProperty(PropertyName = StatePropertyName)]
        [JsonProperty(PropertyName = StatePropertyName)]
        public string state { get; set; }

        public const string AuthorizationCodePropertyName = PinterestProvider.AuthorizationCode;
        [ApiProperty(PropertyName = AuthorizationCodePropertyName)]
        [JsonProperty(PropertyName = AuthorizationCodePropertyName)]
        public string authorizationCode { get; set; }

        [HttpGet(MatchAllParameters = false)]
        public static async Task<HttpResponseMessage> Get(
                [QueryParameter(Name = StatePropertyName)]string state,
                [QueryParameter(Name = AuthorizationCodePropertyName)]string code,
                AzureApplication application,
                HttpRequestMessage request,
                System.Web.Http.Routing.UrlHelper urlHelper,
            RedirectResponse onRedirectResponse,
            BadRequestResponse onBadCredentials,
            HtmlResponse onCouldNotConnect,
            HtmlResponse onGeneralFailure)
        {
            var method = await EastFive.Azure.Auth.Method.ByMethodName(PinterestProvider.IntegrationName, application);
            var requestParams = request.GetQueryNameValuePairs().ToDictionary();
            return await ProcessRequestAsync(method,
                    requestParams,
                    application, request, urlHelper,
                (redirect) =>
                {
                    return onRedirectResponse(redirect);
                },
                (why) => onBadCredentials().AddReason(why),
                (why) =>
                {
                    return onCouldNotConnect(why);
                },
                (why) =>
                {
                    return onGeneralFailure(why);
                });
        }
    }
}

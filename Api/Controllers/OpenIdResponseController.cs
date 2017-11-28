using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

using BlackBarLabs;
using BlackBarLabs.Api;
using EastFive.Api.Services;
using BlackBarLabs.Extensions;
using EastFive.Security.CredentialProvider.AzureADB2C;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class OpenIdConnectResult
    {
        public string id_token { get; set; }

        public string state { get; set; }

        public string error { get; set; }

        public string error_description { get; set; }
    }

    [RoutePrefix("aadb2c")]
    public class OpenIdResponseController : BaseController
    {
        public async Task<IHttpActionResult> Get([FromUri]OpenIdConnectResult result)
        {
            var context = Request.GetSessionServerContext();
            return await context.GetLoginProvider<Task<IHttpActionResult>>(CredentialValidationMethodTypes.Password,
                async (loginProvider) =>
                {
                    var callbackUrl = this.Url.GetLocation<OpenIdResponseController>();
                    if (null == result)
                    {
                        var redirect_uri = "http://orderowl.com/";
                        var loginUrl = loginProvider.GetLoginUrl(redirect_uri, 0, new byte[] { }, callbackUrl);
                        return Redirect(loginUrl);
                    }

                    var parseResult = await loginProvider.ParseState(result.state,
                        (authenticationRequestId) =>
                        {
                            return context.AuthenticationRequests.GetAsync(authenticationRequestId,
                                callbackUrl,
                                (authenticationRequest) =>
                                {
                                    return Redirect(authenticationRequest.loginUrl);
                                },
                                () =>
                                {
                                    var redirect_uri = "http://orderowl.com/";
                                    var loginUrl = loginProvider.GetLoginUrl(redirect_uri, 0, new byte[] { }, callbackUrl);
                                    return Redirect(loginUrl);
                                },
                                (why) =>
                                {
                                    var redirect_uri = "http://orderowl.com/";
                                    var loginUrl = loginProvider.GetLoginUrl(redirect_uri, 0, new byte[] { }, callbackUrl);
                                    return Redirect(loginUrl);
                                });
                            //if (!extraParams.ContainsKey(SessionServer.Configuration.AuthorizationParameters.RedirectUri))
                            //    return Request.CreateResponse(HttpStatusCode.Conflict).AddReason("Redirect URL not in response parameters").ToActionResult();
                            //var redirectUriString = extraParams[SessionServer.Configuration.AuthorizationParameters.RedirectUri];
                            //Uri redirect_uri;
                            //if (!Uri.TryCreate(redirectUriString, UriKind.Absolute, out redirect_uri))
                            //    return Request.CreateResponse(HttpStatusCode.Conflict).AddReason($"Invalid redirect URL in response parameters: {redirectUriString}").ToActionResult();
                            //var loginUrl = loginProvider.GetLoginUrl(redirect_uri.AbsoluteUri, 0, new byte[] { }, callbackUrl);
                            // return Redirect(loginUrl);
                        },
                        (why) =>
                        {
                            var redirect_uri = "http://orderowl.com/";
                            var loginUrl = loginProvider.GetLoginUrl(redirect_uri, 0, new byte[] { }, callbackUrl);
                            return Redirect(loginUrl).ToTask();
                        });
                    return parseResult;
                },
                () => Request.CreateResponse(HttpStatusCode.ServiceUnavailable)
                    .AddReason("AADB2C is not enabled right now")
                    .ToActionResult().ToTask(),
                (why) => Request.CreateResponse(HttpStatusCode.InternalServerError)
                    .AddReason(why)
                    .ToActionResult().ToTask());
        }

        private TResult GetLoginUrl<TResult>(IDictionary<string, string> extraParams, IProvideLogin identityService, Uri callbackUrl,
            Func<Uri, TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            if (!extraParams.ContainsKey(SessionServer.Configuration.AuthorizationParameters.RedirectUri))
                return onFailure("Redirect URL not in response parameters");
            var redirectUriString = extraParams[SessionServer.Configuration.AuthorizationParameters.RedirectUri];
            Uri redirect_uri;
            if (!Uri.TryCreate(redirectUriString, UriKind.Absolute, out redirect_uri))
                return onFailure($"Invalid redirect URL in response parameters: {redirectUriString}");
            var loginUrl = identityService.GetLoginUrl(redirect_uri.AbsoluteUri, 0, new byte[] { }, callbackUrl);
            return onSuccess(loginUrl);
        }

        private async Task<IHttpActionResult> CreateResponse(Guid sessionId, Guid? authorizationId, string jwtToken, string refreshToken,
            IDictionary<string, string> extraParams, Uri redirectUri)
        {
            // Enforce a redirect parameter here since OpenIDCreates on in the state data.
            //if (!extraParams.ContainsKey(SessionServer.Configuration.AuthorizationParameters.RedirectUri))
            //    return Request.CreateResponse(HttpStatusCode.Conflict).AddReason("Redirect URL not in response parameters").ToActionResult();

            var config = Library.configurationManager;
            var redirectResponse = await config.GetRedirectUriAsync(CredentialValidationMethodTypes.Password,
                authorizationId, jwtToken, refreshToken, extraParams, redirectUri,
                (redirectUrl) => Redirect(redirectUrl),
                (paramName, why) => Request.CreateResponse(HttpStatusCode.BadRequest).AddReason(why).ToActionResult(),
                (why) => Request.CreateResponse(HttpStatusCode.BadRequest).AddReason(why).ToActionResult());
            return redirectResponse;
        }

        public async Task<IHttpActionResult> Post(OpenIdConnectResult result)
        {
            if (!String.IsNullOrWhiteSpace(result.error))
                return this.Request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason(result.error_description)
                    .ToActionResult();

            var context = this.Request.GetSessionServerContext();
            var response = await await context.AuthenticationRequests.UpdateAsync<Task<IHttpActionResult>>(Guid.NewGuid(),
                CredentialValidationMethodTypes.Password,
                new Dictionary<string, string>()
                {
                    { AzureADB2CProvider.StateKey, result.state },
                    { AzureADB2CProvider.IdTokenKey, result.id_token }
                },
                (sessionId, authorizationId, jwtToken, refreshToken, extraParams, redirectUri) =>
                {
                    return CreateResponse(sessionId, authorizationId, jwtToken, refreshToken, extraParams, redirectUri);
                },
                (existingId) => this.Request.CreateAlreadyExistsResponse<Controllers.SessionController>(existingId, Url)
                    .ToActionResult()
                    .ToTask(),
                (why) => this.Request.CreateResponse(HttpStatusCode.BadRequest)
                    .AddReason($"Invalid token:{why}")
                    .ToActionResult()
                    .ToTask(),
                () => this.Request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Token is for user is not connected to a user in this system")
                    .ToActionResult()
                    .ToTask(),
                (why) => this.Request.CreateResponse(HttpStatusCode.ServiceUnavailable)
                    .AddReason(why)
                    .ToActionResult()
                    .ToTask(),
                (why) => this.Request.CreateResponse(HttpStatusCode.InternalServerError)
                    .AddReason(why)
                    .ToActionResult()
                    .ToTask(),
                (why) => this.Request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason(why)
                    .ToActionResult()
                    .ToTask());

            return response;
        }
    }
}
// https://login.microsoftonline.com/humatestlogin.onmicrosoft.com/oauth2/authorize?client_id=bb2a2e3a-c5e7-4f0a-88e0-8e01fd3fc1f4&redirect_uri=https:%2f%2flogin.microsoftonline.com%2fte%2fhumatestlogin.onmicrosoft.com%2foauth2%2fauthresp&response_type=id_token&scope=email+openid&response_mode=query&nonce=ZjJlb75S5AoaET4v6TLuxw%3d%3d&  nux=1&nca=1&domain_hint=humatestlogin.onmicrosoft.com&prompt=login&mkt=en-US&lc=1033&state=eyJTSUQiOiJ4LW1zLWNwaW0tcmM6NzJjNzQ2N2ItYTFiMi00MjdjLThlZTgtZDBmMTM3YjNlZGZkIiwiVElEIjoiNjMzZjdiZTktOTAxNy00ZDFkLWJjNWEtOTBmYWM3MWUxNWU3In0
// https://login.microsoftonline.com/humatestlogin.onmicrosoft.com/oauth2/authorize?client_id=bb2a2e3a-c5e7-4f0a-88e0-8e01fd3fc1f4&redirect_uri=https:%2f%2flogin.microsoftonline.com%2fte%2fhumatestlogin.onmicrosoft.com%2foauth2%2fauthresp&response_type=id_token&scope=email+openid&response_mode=query&nonce=zEu4M5xhVG68UMNVV%2busug%3d%3d&nux=1&nca=1&domain_hint=humatestlogin.onmicrosoft.com&prompt=login&mkt=en-US&lc=1033&state=eyJTSUQiOiJ4LW1zLWNwaW0tcmM6ZWFmMzM0MWMtN2ZlOC00MjAxLWExYjgtN2QxMGEwM2M0MzQxIiwiVElEIjoiNjMzZjdiZTktOTAxNy00ZDFkLWJjNWEtOTBmYWM3MWUxNWU3In0

//https://login.microsoftonline.com/fabrikamb2c.onmicrosoft.com/oauth2/v2.0/authorize?
//client_id=90c0fe63-bcf2-44d5-8fb7-b8bbc0b29dc6
//&response_type=code+id_token
//&redirect_uri=https%3A%2F%2Faadb2cplayground.azurewebsites.net%2F
//&response_mode=form_post
//&scope=openid%20offline_access
//&state=arbitrary_data_you_can_receive_in_the_response
//&nonce=12345
//&p=b2c_1_sign_in

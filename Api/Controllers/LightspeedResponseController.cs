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
using System.Web.Http.Routing;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class LightspeedConnectResult
    {
        public string id_token { get; set; }

        public string state { get; set; }

        public string error { get; set; }

        public string error_description { get; set; }
    }
    
    public class LightspeedResponseController : BaseController
    {
        public async Task<IHttpActionResult> Get([FromUri]OpenIdConnectResult result)
        {
            return Request.GetSessionServerContext().GetLoginProvider(CredentialValidationMethodTypes.OAuth,
                (loginProvider) =>
                {
                    var callbackUrl = this.Url.GetLocation<OpenIdResponseController>();
                    if (null == result)
                    {
                        var redirect_uri = "http://orderowl.com/";
                        var loginUrl = loginProvider.GetLoginUrl(redirect_uri, 0, new byte[] { }, callbackUrl);
                        return Redirect(loginUrl);
                    }

                    var parseResult = loginProvider.ParseState(result.state,
                        (action, data, extraParams) =>
                        {
                            if (!extraParams.ContainsKey(SessionServer.Configuration.AuthorizationParameters.RedirectUri))
                                return Request.CreateResponse(HttpStatusCode.Conflict).AddReason("Redirect URL not in response parameters").ToActionResult();
                            var redirectUriString = extraParams[SessionServer.Configuration.AuthorizationParameters.RedirectUri];
                            Uri redirect_uri;
                            if (!Uri.TryCreate(redirectUriString, UriKind.Absolute, out redirect_uri))
                                return Request.CreateResponse(HttpStatusCode.Conflict).AddReason($"Invalid redirect URL in response parameters: {redirectUriString}").ToActionResult();
                            var loginUrl = loginProvider.GetLoginUrl(redirect_uri.AbsoluteUri, 0, new byte[] { }, callbackUrl);
                            return Redirect(loginUrl);
                        },
                        (why) =>
                        {
                            var redirect_uri = "http://orderowl.com/";
                            var loginUrl = loginProvider.GetLoginUrl(redirect_uri, 0, new byte[] { }, callbackUrl);
                            return Redirect(loginUrl);
                        });
                    return parseResult;
                },
                () => Request.CreateResponse(HttpStatusCode.ServiceUnavailable).AddReason("AADB2C is not enabled right now").ToActionResult(),
                (why) => Request.CreateResponse(HttpStatusCode.InternalServerError).AddReason(why).ToActionResult());
        }
        
        private async Task<IHttpActionResult> CreateResponse(Guid sessionId, Guid? authorizationId, string jwtToken, string refreshToken, IDictionary<string, string> extraParams)
        {
            // Enforce a redirect parameter here since OpenIDCreates one in the state data.
            if (!extraParams.ContainsKey(SessionServer.Configuration.AuthorizationParameters.RedirectUri))
                return Request.CreateResponse(HttpStatusCode.Conflict).AddReason("Redirect URL not in response parameters").ToActionResult();

            var config = Library.configurationManager;
            var redirectResponse = await config.GetRedirectUriAsync<IHttpActionResult>(CredentialValidationMethodTypes.Password,
                authorizationId, jwtToken, refreshToken, extraParams, default(Uri),
                (redirectUrl) => Redirect(redirectUrl),
                (paramName, why) => Request.CreateResponse(HttpStatusCode.BadRequest).AddReason(why).ToActionResult(),
                (why) => Request.CreateResponse(HttpStatusCode.BadRequest).AddReason(why).ToActionResult());
            return redirectResponse;
        }

        public async Task<IHttpActionResult> Post(OpenIdConnectResult result)
        {
            throw new NotImplementedException();
        }
    }
}
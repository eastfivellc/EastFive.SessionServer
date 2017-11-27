using BlackBarLabs.Api;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using BlackBarLabs;
using BlackBarLabs.Extensions;
using EastFive.Api.Services;

namespace EastFive.Security.SessionServer.Api.Controllers
{

    public class AccountLinksQuery
    {
        [JsonProperty(PropertyName = "redirect_uri")]
        public string redirect_uri { get; set; }

        [JsonProperty(PropertyName = "response_mode")]
        public string response_mode { get; set; }
    }

    [RoutePrefix("aadb2c")]
    public class AccountLinksController : BaseController
    {
        [HttpGet]
        public async Task<IHttpActionResult> Get([FromUri]AccountLinksQuery q)
        {
            var response_mode = q.response_mode;
            var redirect_uri = q.redirect_uri;

            var context = this.Request.GetSessionServerContext();
            if (String.IsNullOrWhiteSpace(redirect_uri))
            {
                return this.Request.CreateRedirectResponse<Controllers.AuthenticationRequestLinkController>(Url).ToActionResult();
            }
            //return new HttpActionResult(() => this.Request
            //    .CreateResponse(System.Net.HttpStatusCode.BadRequest)
            //    .AddReason("Missing redirect_uri parameter")
            //    .ToTask());
            
            return context.GetLoginProvider(CredentialValidationMethodTypes.Password,
                (loginProvider) =>
                {
                    var callbackUrl = this.Url.GetLocation<OpenIdResponseController>(
                        typeof(OpenIdResponseController)
                            .GetCustomAttributes<RoutePrefixAttribute>()
                            .Select(routePrefix => routePrefix.Prefix)
                            .First());
                    var response = this.Request.CreateResponse(System.Net.HttpStatusCode.OK,
                        new Resources.AccountLink
                        {
                            Login = loginProvider.GetLoginUrl(redirect_uri, 0, new byte[] { }, callbackUrl),
                            Signup = loginProvider.GetSignupUrl(redirect_uri, 0, new byte[] { }, callbackUrl),
                            Logout = loginProvider.GetLogoutUrl(redirect_uri, 0, new byte[] { }, callbackUrl),
                        });
                    return response;
                },
                () => Request.CreateResponse(System.Net.HttpStatusCode.InternalServerError).AddReason("AADB2C login is not enabled"),
                (why) => Request.CreateResponse(System.Net.HttpStatusCode.InternalServerError).AddReason(why))
                .ToActionResult();
        }
    }
}
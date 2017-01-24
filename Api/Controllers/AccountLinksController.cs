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
    public class AccountLinks
    {
        [JsonProperty(PropertyName ="login")]
        public Uri Login { get; set; }

        [JsonProperty(PropertyName = "signup")]
        public Uri Signup { get; set; }

        [JsonProperty(PropertyName = "logout")]
        public Uri Logout { get; set; }
    }

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

            var loginProviderTaskGetter = (Func<Task<IIdentityService>>)
                this.Request.Properties[ServicePropertyDefinitions.IdentityService];
            var loginProviderTask = loginProviderTaskGetter();
            var loginProvider = await loginProviderTask;
            var callbackUrl = this.Url.GetLocation<OpenIdResponseController>(
                        typeof(OpenIdResponseController)
                            .GetCustomAttributes<RoutePrefixAttribute>()
                            .Select(routePrefix => routePrefix.Prefix)
                            .First());
            return this.Request.CreateResponse(System.Net.HttpStatusCode.OK,
                new AccountLinks
                {
                    Login = loginProvider.GetLoginUrl(redirect_uri, 0, new byte[] { }, callbackUrl),
                    Signup = loginProvider.GetSignupUrl(redirect_uri, 0, new byte[] { }, callbackUrl),
                    Logout = loginProvider.GetLogoutUrl(redirect_uri, 0, new byte[] { }, callbackUrl),
                }).ToActionResult();
        }
    }
}
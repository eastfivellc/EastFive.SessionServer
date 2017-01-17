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

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class AccountLinks
    {
        [JsonProperty(PropertyName ="login")]
        public Uri Login { get; set; }

        [JsonProperty(PropertyName = "signup")]
        public Uri Signup { get; set; }
    }

    public class AccountLinksQuery
    {
        [JsonProperty(PropertyName = "redirect_uri")]
        public string redirect_uri { get; set; }

        [JsonProperty(PropertyName = "response_mode")]
        public string response_mode { get; set; }
    }

    [RoutePrefix("aadb2c")]
    public class AccountLinksController : ApiController
    {
        public static string SignupEndpoint;
        public static string SigninEndpoint;
        public static string Audience;

        [HttpGet]
        public IHttpActionResult Get([FromUri]AccountLinksQuery q)
        {
            var response_mode = q.response_mode;
            var redirect_uri = q.redirect_uri;
            
            return this.Request.CreateResponse(System.Net.HttpStatusCode.OK,
                new AccountLinks
                {
                    Login = GetUrl(AccountLinksController.SigninEndpoint, redirect_uri, response_mode),
                    Signup = GetUrl(AccountLinksController.SignupEndpoint, redirect_uri, response_mode),
                }).ToActionResult();
        }

        private Uri GetUrl(string longurl, string redirect_uri, string response_mode)
        {
            var uriBuilder = new UriBuilder(longurl);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["client_id"] = AccountLinksController.Audience;
            query["response_type"] = "id_token";
            query["redirect_uri"] = String.IsNullOrWhiteSpace(redirect_uri) ?
                this.Url.GetLocation<OpenIdResponseController>(
                    typeof(OpenIdResponseController)
                        .GetCustomAttributes<RoutePrefixAttribute>()
                        .Select(routePrefix => routePrefix.Prefix)
                        .First()).AbsoluteUri
                :
                redirect_uri;
            query["response_mode"] = String.IsNullOrWhiteSpace(response_mode) ? "form_post" : response_mode;
            query["scope"] = "openid";
            query["state"] = Guid.NewGuid().ToString("N");
            query["nonce"] = Guid.NewGuid().ToString("N");
            // query["p"] = "B2C_1_signin1";
            uriBuilder.Query = query.ToString();
            var redirect = uriBuilder.Uri; // .ToString();
            return redirect;
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

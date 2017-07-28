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
using System.Xml;
using BlackBarLabs.Extensions;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class SAMLConnectResult
    {
        public string id_token { get; set; }

        public string state { get; set; }

        public string error { get; set; }

        public string error_description { get; set; }
    }

    // aadb2c/SAMLRedirect?tokenid=ID7ee36a406286079a7237b23dd7647d95b8d42ddbcde4fbe8030000015d5790a407&agentid=e924bba8
    [RoutePrefix("aadb2c")]
    public class SAMLRedirectController : BaseController
    {
        public async Task<IHttpActionResult> Post()
        {
            //return await await this.Request.Content.ParseMultipartAsync(
            //    (string SAMLResponse) =>
            //    {
            //        return ParseSAMLResponseAsync(SAMLResponse);
            //    });
            return await await this.Request.Content.ParseMultipartAsync(
                (byte[] SAMLResponse) => ParseSAMLResponseAsync(SAMLResponse));
        }

        private async Task<IHttpActionResult> ParseSAMLResponseAsync(byte[] samlResponseBytes)
        {
            var samlResponse = System.Text.Encoding.UTF8.GetString(samlResponseBytes);
            if (String.IsNullOrWhiteSpace(samlResponse))
                return this.Request.CreateResponse(HttpStatusCode.Conflict)
                            .AddReason("SAML Response not provided in form POST")
                            .ToActionResult();
            var context = Request.GetSessionServerContext();
            var response = await context.Sessions.CreateAsync(Guid.NewGuid(),
                CredentialValidationMethodTypes.SAML,
                samlResponse,
                (authorizationId, token, refreshToken, extraParams) =>
                {
                    var config = Library.configurationManager;
                    var redirectResponse = config.GetRedirectUri<IHttpActionResult>(CredentialValidationMethodTypes.SAML,
                        authorizationId, token, refreshToken, extraParams,
                        (redirectUrl) => Redirect(redirectUrl),
                        (paramName, why) => Request.CreateResponse(HttpStatusCode.BadRequest).AddReason(why).ToActionResult(),
                        (why) => Request.CreateResponse(HttpStatusCode.BadRequest).AddReason(why).ToActionResult());
                    return redirectResponse;
                },
                () => this.Request.CreateResponse(HttpStatusCode.Conflict)
                            .AddReason("Already exists")
                            .ToActionResult(),
                (why) => this.Request.CreateResponse(HttpStatusCode.BadRequest)
                            .AddReason($"Invalid token:{why}")
                            .ToActionResult(),
                () => this.Request.CreateResponse(HttpStatusCode.Conflict)
                            .AddReason("Token does not work in this system")
                            .ToActionResult(),
                () => this.Request.CreateResponse(HttpStatusCode.Conflict)
                            .AddReason("Token is not connected to a user in this system")
                            .ToActionResult(),
                (why) => this.Request.CreateResponse(HttpStatusCode.BadGateway)
                            .AddReason(why)
                            .ToActionResult(),
                (why) => this.Request.CreateResponse(HttpStatusCode.ServiceUnavailable)
                            .AddReason(why)
                            .ToActionResult());

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

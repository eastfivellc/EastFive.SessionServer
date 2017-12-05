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
    // /api/OAuthResponseLightspeed?code=d6ac033707089b1a727711631c277323b7c7905a&state=331d23d2cc204a1b9a7eeb1420000333
    public class OAuthResponseLightspeedController : ResponseController
    {
        public override async Task<IHttpActionResult> Get([FromUri]ResponseResult result)
        {
            result.method = CredentialValidationMethodTypes.Lightspeed;
            return await base.Get(result);
        }

        //private async Task<IHttpActionResult> CreateResponse(Guid sessionId, Guid? authorizationId, string jwtToken, string refreshToken, IDictionary<string, string> extraParams)
        //{
        //    // Enforce a redirect parameter here since OpenIDCreates one in the state data.
        //    if (!extraParams.ContainsKey(SessionServer.Configuration.AuthorizationParameters.RedirectUri))
        //        return Request.CreateResponse(HttpStatusCode.Conflict).AddReason("Redirect URL not in response parameters").ToActionResult();

        //    var config = Library.configurationManager;
        //    var redirectResponse = await config.GetRedirectUriAsync<IHttpActionResult>(CredentialValidationMethodTypes.Password,
        //        authorizationId, jwtToken, refreshToken, extraParams, default(Uri),
        //        (redirectUrl) => Redirect(redirectUrl),
        //        (paramName, why) => Request.CreateResponse(HttpStatusCode.BadRequest).AddReason(why).ToActionResult(),
        //        (why) => Request.CreateResponse(HttpStatusCode.BadRequest).AddReason(why).ToActionResult());
        //    return redirectResponse;
        //}

        //public async Task<IHttpActionResult> Post(OpenIdConnectResult result)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
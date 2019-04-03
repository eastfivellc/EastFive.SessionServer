using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using BlackBarLabs.Api.Controllers;

using BlackBarLabs;
using BlackBarLabs.Api;
using EastFive.Api.Services;
using System.Xml;
using BlackBarLabs.Extensions;
using System.Text;
using System.IO;
using System.Net.Http.Headers;
using Microsoft.ApplicationInsights;
using EastFive.Security.SessionServer.Configuration;
using EastFive.Security.SessionServer.Exceptions;
using EastFive.Extensions;
using EastFive.Api.Controllers;
using EastFive.Collections.Generic;
using Newtonsoft.Json;
using EastFive.Azure.Auth;

namespace EastFive.Api.Azure.Credentials.Controllers
{
    [RoutePrefix("aadb2c")]
    [FunctionViewController4(
        Route="PingResponse",
        Resource = typeof(PingResponse),
        ContentType = "x-application/ping-response",
        ContentTypeVersion = "0.1")]
    public class PingResponse : EastFive.Azure.Auth.Redirection
    {
        public const string TokenIdPropertyName = PingProvider.TokenId;
        [ApiProperty(PropertyName = TokenIdPropertyName)]
        [JsonProperty(PropertyName = TokenIdPropertyName)]
        public string tokenid { get; set; }

        public const string AgentIdPropertyName = PingProvider.AgentId;
        [ApiProperty(PropertyName = AgentIdPropertyName)]
        [JsonProperty(PropertyName = AgentIdPropertyName)]
        public string agentid { get; set; }

        [HttpGet(MatchAllParameters = false)]
        public static async Task<HttpResponseMessage> Get(
                [QueryParameter(Name = TokenIdPropertyName)]string tokenId,
                [QueryParameter(Name = AgentIdPropertyName)]string agentId,
                AzureApplication application,
                HttpRequestMessage request,
                System.Web.Http.Routing.UrlHelper urlHelper,
            RedirectResponse onRedirectResponse,
            BadRequestResponse onBadRequest)
        {
            //The way this works...
            //1.  User clicks Third Party Applications\AffirmHealth over in Athena.
            //2.  Athena calls Ping
            //3.  Ping redirects to /PingResponseController with a token.
            //4.  This code validates the token, parses it out, and redirects to the interactive report matching the patient id.

            //To debug, you have to grab the token from Ping that comes in here.  If you don't, the token will get used and it won't work again
            //To do this, uncomment the commented line and comment out the call to ParsePingResponseAsync.  That way the token won't be used.
            //After the uncomment/comment, publish to dev and then click third party apps\Affirm Health in Athena.
            //Grab the token from the browser.
            //Then, switch the uncommented/commented lines back and run the server in debug.
            //Send the token via Postman to debug and see any errors that might come back from Ping.

            //return request.CreateResponse(HttpStatusCode.OK).ToTask();
            //return ParsePingResponseAsync(query.tokenid, query.agentid);

            var methodName = Enum.GetName(typeof(CredentialValidationMethodTypes), CredentialValidationMethodTypes.Ping);
            var method = await EastFive.Azure.Auth.Method.ByMethodName(methodName, application);
            return await Redirection.ProcessRequestAsync(method, 
                    request.GetQueryNameValuePairs().ToDictionary(), 
                    application, request, urlHelper,
                (redirect, why) => onRedirectResponse(redirect, "success"),
                (why) => onBadRequest().AddReason(why));
        }
    }
}
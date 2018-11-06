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

namespace EastFive.Api.Azure.Credentials.Controllers
{
    public class PingRequest
    {
        public string tokenid { get; set; }

        public string agentid { get; set; }
    }
    
    [RoutePrefix("aadb2c")]
    [FunctionViewController(Route="PingResponse")]
    public class PingResponseController
    {
        [HttpGet]
        public static Task<HttpResponseMessage> Get(
            [QueryParameter(Name ="tokenid")]string tokenId,
            [QueryParameter(Name = "agentid")]string agentId,
            AzureApplication application, HttpRequestMessage request,
            RedirectResponse redirectResponse)
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

            return ResponseController.ProcessRequestAsync(application,
                Enum.GetName(typeof(CredentialValidationMethodTypes), CredentialValidationMethodTypes.Ping),
                request.RequestUri,
                request.GetQueryNameValuePairs().ToDictionary(),
                (redirect, why) => redirectResponse(redirect, why),
                (httpStatusCode, message, reason) => request.CreateResponse(httpStatusCode, message).AddReason(reason));
        }
    }
}
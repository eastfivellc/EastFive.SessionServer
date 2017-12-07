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

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class PingRequest
    {
        public string tokenid { get; set; }

        public string agentid { get; set; }
    }
    
    [RoutePrefix("aadb2c")]
    public class PingResponseController : BaseController
    {
        public Task<IHttpActionResult> Get([FromUri]PingRequest query)
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

            //return ((IHttpActionResult)(new HttpActionResult(() => Request.CreateResponse(HttpStatusCode.OK).ToTask()))).ToTask();
            return ParsePingResponseAsync(query.tokenid, query.agentid);
        }

        private async Task<IHttpActionResult> ParsePingResponseAsync(string tokenId, string agentId)
        {
            var telemetry = Web.Configuration.Settings.GetString(SessionServer.Configuration.AppSettings.ApplicationInsightsKey,
                (applicationInsightsKey) =>
                {
                    return new TelemetryClient { InstrumentationKey = applicationInsightsKey };
                },
                (why) =>
                {
                    return new TelemetryClient();
                });

            if (String.IsNullOrWhiteSpace(agentId))
            {
                telemetry.TrackException(new PingResponseException("PING Response did not include agentId"));
                return this.Request.CreateResponse(HttpStatusCode.Conflict)
                            .AddReason("PING Response did not include agentId")
                            .ToActionResult();
            }

            if (String.IsNullOrWhiteSpace(tokenId))
            {
                telemetry.TrackException(new PingResponseException("PING Response did not include tokenId"));
                return this.Request.CreateResponse(HttpStatusCode.Conflict)
                            .AddReason("PING Response did not include tokenId")
                            .ToActionResult();
            }

            var context = Request.GetSessionServerContext();
            var sessionId = Guid.NewGuid();
            var response = await await context.Sessions.UpdateResponseAsync(sessionId,
                CredentialValidationMethodTypes.Ping,
                new Dictionary<string, string>()
                {
                    {
                        Security.CredentialProvider.Ping.PingProvider.AgentId,
                        agentId
                    },
                    {
                        Security.CredentialProvider.Ping.PingProvider.TokenId,
                        tokenId
                    }
                },
                (requestId, authorizationId, token, refreshToken, action, extraParams, redirectUri) =>
                {
                    telemetry.TrackEvent("PingSessionCreated", new Dictionary<string, string> { {"authorizationId", authorizationId.ToString() } });
                    telemetry.TrackEvent("PingSessionCreated - ExtraParams", extraParams);
                    var config = Library.configurationManager;
                    var redirectResponse = config.GetRedirectUriAsync(context, CredentialValidationMethodTypes.Ping, action,
                        requestId, authorizationId, token, refreshToken, extraParams, redirectUri,
                        (redirectUrl) => Redirect(redirectUrl),
                        (paramName, why) => Request.CreateResponse(HttpStatusCode.BadRequest).AddReason(why).ToActionResult(),
                        (why) => Request.CreateResponse(HttpStatusCode.BadRequest).AddReason(why).ToActionResult());
                    return redirectResponse;
                },
                (existingId) =>
                {
                    telemetry.TrackException(new PingResponseException($"Could not create session because the GUID is not unique (session already exists)"));
                    return this.Request.CreateResponse(HttpStatusCode.InternalServerError)
                        .AddReason("GUID NOT UNIQUE")
                        .ToActionResult()
                        .ToTask();
                },
                (why) =>
                {
                    telemetry.TrackException(new PingResponseException($"Invalid token:{why}"));
                    return this.Request.CreateResponse(HttpStatusCode.BadRequest)
                        .AddReason($"Invalid token:{why}")
                        .ToActionResult()
                        .ToTask();
                },
                () =>
                {
                    telemetry.TrackException(new PingResponseException("Token is not connected to a user in this system"));
                    return this.Request.CreateResponse(HttpStatusCode.Conflict)
                        .AddReason("Token is not connected to a user in this system")
                        .ToActionResult()
                        .ToTask();
                },
                (why) =>
                {
                    telemetry.TrackException(new PingResponseException($"Cannot create session because service is unavailable: {why}"));
                    return this.Request.CreateResponse(HttpStatusCode.ServiceUnavailable)
                        .AddReason(why)
                        .ToActionResult()
                        .ToTask();
                },
                (why) =>
                {
                    telemetry.TrackException(new PingResponseException($"Cannot create session because service is unavailable: {why}"));
                    return this.Request.CreateResponse(HttpStatusCode.ServiceUnavailable)
                        .AddReason(why)
                        .ToActionResult()
                        .ToTask();
                },
                (why) =>
                {
                    telemetry.TrackException(new PingResponseException($"General failure: {why}"));
                    return this.Request.CreateResponse(HttpStatusCode.Conflict)
                        .AddReason(why)
                        .ToActionResult()
                        .ToTask();
                });

            return response;
        }


    }
}
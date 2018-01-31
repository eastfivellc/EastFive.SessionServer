using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using BlackBarLabs;
using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using EastFive.Collections.Generic;
using EastFive.Security.SessionServer.Exceptions;
using System.Web.Http;
using Microsoft.ApplicationInsights;
using EastFive.Extensions;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class ResponseResult
    {
        public CredentialValidationMethodTypes method { get; set; }
    }

    [RoutePrefix("aadb2c")]
    public class ResponseController : BaseController
    {
        public virtual async Task<IHttpActionResult> Get([FromUri]ResponseResult result)
        {
            if (result.IsDefault())
                return this.Request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason("Method not provided in response")
                    .ToActionResult();

            var kvps = Request.GetQueryNameValuePairs();
            return await ProcessRequestAsync(result.method, kvps.ToDictionary());
        }

        public virtual async Task<IHttpActionResult> Post([FromUri]ResponseResult result)
        {
            if (result.IsDefault())
                return this.Request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason("Method not provided in response")
                    .ToActionResult();

            var kvps = Request.GetQueryNameValuePairs();
            var bodyValues = await await Request.Content.ReadFormDataAsync(
                (values) => values.AllKeys
                    .Select(v => v.PairWithValue(values[v]))
                    .ToArray()
                    .ToTask(),
                async () => await await Request.Content.ReadMultipartContentAsync(
                    values => values
                        .Select(async v => v.Key.PairWithValue(await v.Value.ReadAsStringAsync()))
                        .WhenAllAsync(),
                    () => (new KeyValuePair<string, string>()).AsArray().ToTask()));
            var allrequestParams = kvps.Concat(bodyValues).ToDictionary();
            return await ProcessRequestAsync(result.method, allrequestParams);
        }

        protected async Task<IHttpActionResult> ProcessRequestAsync(CredentialValidationMethodTypes method,
            IDictionary<string, string> values)
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

            var context = this.Request.GetSessionServerContext();
            var response = await await context.Sessions.UpdateWithAuthenticationAsync(Guid.NewGuid(),
                    method, values,
                (sessionId, authorizationId, jwtToken, refreshToken, action, extraParams, redirectUrl) =>
                    CreateResponse(context, method, action, sessionId, authorizationId, jwtToken, refreshToken, extraParams, redirectUrl, telemetry),
                (location) =>
                {
                    if (location.IsDefaultOrNull())
                        return Web.Configuration.Settings.GetUri(SessionServer.Configuration.AppSettings.LandingPage,
                            (redirect) => ((IHttpActionResult)Redirect(location))
                                .ToTask(),
                            (why) => this.Request.CreateResponse(HttpStatusCode.BadRequest, why)
                                .AddReason($"Location was null")
                                .ToActionResult()
                                .ToTask());
                    if (location.Query.IsNullOrWhiteSpace())
                        location = location.SetQueryParam("cache", Guid.NewGuid().ToString("N"));
                    return ((IHttpActionResult)Redirect(location)).ToTask();
                },
                (why) =>
                {
                    var message = $"Invalid token:{why}";
                    telemetry.TrackException(new ResponseException());
                    return this.Request.CreateResponse(HttpStatusCode.BadRequest, message)
                        .AddReason($"Invalid token:{why}")
                        .ToActionResult()
                        .ToTask();
                },
                () =>
                {
                    var message = "Token is not connected to a user in this system";
                    telemetry.TrackException(new ResponseException(message));
                    return this.Request.CreateResponse(HttpStatusCode.Conflict)
                        .AddReason(message)
                        .ToActionResult()
                        .ToTask();
                },
                (why) =>
                {
                    var message = $"Cannot create session because service is unavailable: {why}";
                    telemetry.TrackException(new ResponseException(message));
                    return this.Request.CreateResponse(HttpStatusCode.ServiceUnavailable, message)
                        .AddReason(why)
                        .ToActionResult()
                        .ToTask();
                },
                (why) =>
                {
                    var message = $"Cannot create session because service is unavailable: {why}";
                    telemetry.TrackException(new ResponseException(message));
                    return this.Request.CreateResponse(HttpStatusCode.ServiceUnavailable, message)
                        .AddReason(why)
                        .ToActionResult()
                        .ToTask();
                },
                (why) =>
                {
                    var message = $"General failure: {why}";
                    telemetry.TrackException(new ResponseException(message));
                    return this.Request.CreateResponse(HttpStatusCode.Conflict, message)
                        .AddReason(why)
                        .ToActionResult()
                        .ToTask();
                });
            return response;
        }

        private async Task<IHttpActionResult> CreateResponse(Context context,
            CredentialValidationMethodTypes method, AuthenticationActions action,
            Guid sessionId, Guid? authorizationId, string jwtToken, string refreshToken,
            IDictionary<string, string> extraParams, Uri redirectUrl, TelemetryClient telemetry)
        {
            var config = Library.configurationManager;
            var redirectResponse = await config.GetRedirectUriAsync(context, method, action,
                    sessionId, authorizationId, jwtToken, refreshToken, extraParams,
                    redirectUrl,
                (redirectUrlSelected) => Redirect(redirectUrlSelected),
                (paramName, why) =>
                {
                    var message = $"Invalid parameter while completing login: {paramName} - {why}";
                    telemetry.TrackException(new ResponseException(message));
                    return Request.CreateResponse(HttpStatusCode.BadRequest, message)
                        .AddReason(why)
                        .ToActionResult();
                },
                (why) =>
                {
                    var message = $"General failure while completing login: {why}";
                    telemetry.TrackException(new ResponseException(message));
                    return Request.CreateResponse(HttpStatusCode.BadRequest, message)
                        .AddReason(why)
                            .ToActionResult();
                });
            return redirectResponse;
        }
    }
}

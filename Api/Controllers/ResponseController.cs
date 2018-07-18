using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using BlackBarLabs;
using EastFive;
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
            return await ProcessRequestAsync(Enum.GetName(typeof(CredentialValidationMethodTypes), result.method), kvps.ToDictionary(),
                location => Redirect(location),
                (code, body, reason) => this.Request.CreateResponse(code, body)
                        .AddReason(reason)
                        .ToActionResult());
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
                    values => BlackBarLabs.TaskExtensions.WhenAllAsync(values
                        .Select(async v => v.Key.PairWithValue(await v.Value.ReadAsStringAsync()))),
                    () => (new KeyValuePair<string, string>()).AsArray().ToTask()));
            var allrequestParams = kvps.Concat(bodyValues).ToDictionary();
            return await ProcessRequestAsync(Enum.GetName(typeof(CredentialValidationMethodTypes), result.method), allrequestParams,
                location => Redirect(location),
                (code, body, reason) => this.Request.CreateResponse(code, body)
                        .AddReason(reason)
                        .ToActionResult());
        }

        protected static async Task<TResult> ProcessRequestAsync<TResult>(string method, IDictionary<string, string> values,
            Func<Uri, TResult> onRedirect,
            Func<HttpStatusCode, string, string, TResult> onResponse)
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

            var context = Context.LoadFromConfiguration();
            var response = await await context.Sessions.CreateOrUpdateWithAuthenticationAsync<Task<TResult>>(
                    method, values,
                async (sessionId, authorizationId, jwtToken, refreshToken, action, extraParams, redirectUrl) =>
                {
                    telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - Created Authentication.  Creating response.");
                    var resp = await CreateResponse(context, method, action, sessionId, authorizationId, jwtToken, refreshToken, extraParams, redirectUrl, onRedirect, onResponse, telemetry);
                    return resp;
                },
                (location) =>
                {
                    telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - location: {location.AbsolutePath}");
                    if (location.IsDefaultOrNull())
                        return Web.Configuration.Settings.GetUri(SessionServer.Configuration.AppSettings.LandingPage,
                            (redirect) => onRedirect(location),
                            (why) => onResponse(HttpStatusCode.BadRequest, why, $"Location was null")).ToTask();
                    if (location.Query.IsNullOrWhiteSpace())
                        location = location.SetQueryParam("cache", Guid.NewGuid().ToString("N"));
                    return onRedirect(location).ToTask();
                },
                (why) =>
                {
                    var message = $"Invalid token:{why}";
                    telemetry.TrackException(new ResponseException());
                    return onResponse(HttpStatusCode.BadRequest, message, $"Invalid token:{why}").ToTask();
                },
                () =>
                {
                    var message = "Token is not connected to a user in this system";
                    telemetry.TrackException(new ResponseException(message));
                    return onResponse(HttpStatusCode.Conflict, message, message).ToTask();
                },
                (why) =>
                {
                    var message = $"Cannot create session because service is unavailable: {why}";
                    telemetry.TrackException(new ResponseException(message));
                    return onResponse(HttpStatusCode.ServiceUnavailable, message, why).ToTask();
                },
                (why) =>
                {
                    var message = $"Cannot create session because service is unavailable: {why}";
                    telemetry.TrackException(new ResponseException(message));
                    return onResponse(HttpStatusCode.ServiceUnavailable, message, why).ToTask();
                },
                (why) =>
                {
                    var message = $"General failure: {why}";
                    telemetry.TrackException(new ResponseException(message));
                    return onResponse(HttpStatusCode.Conflict, message, why).ToTask();
                });
            return response;
        }

        private static async Task<TResult> CreateResponse<TResult>(Context context,
            string method, AuthenticationActions action,
            Guid sessionId, Guid? authorizationId, string jwtToken, string refreshToken,
            IDictionary<string, string> extraParams, Uri redirectUrl,
            Func<Uri, TResult> onRedirect,
            Func<HttpStatusCode, string, string, TResult> onResponse,
            TelemetryClient telemetry)
        {
            var config = Library.configurationManager;
            var redirectResponse = await config.GetRedirectUriAsync(context, method, action,
                    sessionId, authorizationId, jwtToken, refreshToken, extraParams,
                    redirectUrl,
                (redirectUrlSelected) =>
                {
                    telemetry.TrackEvent($"CreateResponse - redirectUrlSelected1: {redirectUrlSelected.AbsolutePath}");
                    telemetry.TrackEvent($"CreateResponse - redirectUrlSelected2: {redirectUrlSelected.AbsoluteUri}");
                    return onRedirect(redirectUrlSelected);
                },
                (paramName, why) =>
                {
                    var message = $"Invalid parameter while completing login: {paramName} - {why}";
                    telemetry.TrackException(new ResponseException(message));
                    return onResponse(HttpStatusCode.BadRequest, message, why);
                },
                (why) =>
                {
                    var message = $"General failure while completing login: {why}";
                    telemetry.TrackException(new ResponseException(message));
                    return onResponse(HttpStatusCode.BadRequest, message, why);
                });

            var msg = redirectResponse;
            telemetry.TrackEvent($"CreateResponse - {msg}");
            return redirectResponse;
        }
    }
}

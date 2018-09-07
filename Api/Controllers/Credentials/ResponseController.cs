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
using EastFive.Security.SessionServer;

namespace EastFive.Api.Azure.Credentials.Controllers
{
    public class ResponseResult
    {
        public Credentials.CredentialValidationMethodTypes method { get; set; }
    }

    [RoutePrefix("aadb2c")]
    public class ResponseController : Azure.Controllers.BaseController
    {
        public virtual async Task<IHttpActionResult> Get([FromUri]ResponseResult result)
        {
            if (result.IsDefault())
                return this.Request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason("Method not provided in response")
                    .ToActionResult();
            
            var kvps = Request.GetQueryNameValuePairs();

            return await Request.GetApplication(
                httpApp => ProcessRequestAsync(httpApp as AzureApplication, Enum.GetName(typeof(CredentialValidationMethodTypes), result.method), kvps.ToDictionary(),
                    (location, why) => Redirect(location),
                    (code, body, reason) => this.Request.CreateResponse(code, body)
                        .AddReason(reason)
                        .ToActionResult()),
                () => this.Request.CreateResponse(HttpStatusCode.OK, "Application is not an EastFive.Azure application.").ToActionResult().ToTask());
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

            return await Request.GetApplication(
                httpApp => ProcessRequestAsync(httpApp as AzureApplication, Enum.GetName(typeof(CredentialValidationMethodTypes), result.method), allrequestParams,
                    (location, why) => Redirect(location),
                    (code, body, reason) => this.Request.CreateResponse(code, body)
                        .AddReason(reason)
                        .ToActionResult()),
                () => this.Request.CreateResponse(HttpStatusCode.OK, "Application is not an EastFive.Azure application.").ToActionResult().ToTask());
        }
        
        public static async Task<TResult> ProcessRequestAsync<TResult>(AzureApplication application, string method, IDictionary<string, string> values,
            Func<Uri, string, TResult> onRedirect,
            Func<HttpStatusCode, string, string, TResult> onResponse)
        {
            var saveAuthLogTask = application.LogAuthorizationRequestAsync(method, values);

            var telemetry = application.Telemetry;
            var context = application.AzureContext;

            var response = await await context.Sessions.CreateOrUpdateWithAuthenticationAsync(
                    application, method, values,
                async (sessionId, authorizationId, jwtToken, refreshToken, action, provider, extraParams, redirectUrl) =>
                {
                    var saveAuthLogAsync = await saveAuthLogTask;
                    var updatingAuthLogTask = saveAuthLogAsync(true, $"Login:{authorizationId}/{sessionId}[{action}]", extraParams);
                    telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - Created Authentication.  Creating response.");
                    var resp = CreateResponse(application, provider, method, action, sessionId, authorizationId, jwtToken, refreshToken, extraParams, redirectUrl, onRedirect, onResponse, telemetry);
                    await updatingAuthLogTask;
                    return await resp;
                },
                async (location, reason, provider, extraParams) =>
                {
                    var saveAuthLogAsync = await saveAuthLogTask;
                    await saveAuthLogAsync(true, $"Logout:{location} -- {reason}", extraParams);
                    telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - location: {location.AbsolutePath}");
                    if (location.IsDefaultOrNull())
                        return Web.Configuration.Settings.GetUri(Security.SessionServer.Configuration.AppSettings.LandingPage,
                            (redirect) => onRedirect(location, reason),
                            (why) => onResponse(HttpStatusCode.BadRequest, why, $"Location was null"));
                    if (location.Query.IsNullOrWhiteSpace())
                        location = location.SetQueryParam("cache", Guid.NewGuid().ToString("N"));
                    return onRedirect(location, reason);
                },
                async (subject, credentialProvider, extraParams, createMappingAsync) =>
                {
                    telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - Token is not connected to a user in this system.");
                    var saveAuthLogAsync = await saveAuthLogTask;
                    var updatingAuthLogTask = saveAuthLogAsync(true, $"Login:{subject}/{credentialProvider.GetType().FullName}", extraParams);
                    return await application.OnUnmappedUserAsync<Task<TResult>>(method, credentialProvider, subject, values,
                        async (authorizationId) =>
                        {
                            await updatingAuthLogTask;
                            telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - Creating Authentication.");
                            updatingAuthLogTask = saveAuthLogAsync(true, $"New user mapping requested:{subject}/{credentialProvider.GetType().FullName}[{authorizationId}]", extraParams);
                            return await await createMappingAsync(authorizationId,
                                async (sessionId, jwtToken, refreshToken, action, redirectUrl) =>
                                {
                                    await updatingAuthLogTask;
                                    await saveAuthLogAsync(true, $"New user mapping requested:{subject}/{credentialProvider.GetType().FullName}[{authorizationId}]", extraParams);
                                    telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - Created Authentication.  Creating response.");
                                    var resp = CreateResponse(application, credentialProvider, method, action, sessionId, authorizationId, jwtToken, refreshToken, extraParams, redirectUrl, onRedirect, onResponse, telemetry);
                                    await updatingAuthLogTask;
                                    return resp;
                                },
                                async (why) =>
                                {
                                    await updatingAuthLogTask;
                                    await saveAuthLogAsync(true, $"Failure to create user mapping requested:{subject}/{credentialProvider.GetType().FullName}[{authorizationId}]: {why}", extraParams);
                                    var message = $"Failure to connect token to a user in this system: {why}";
                                    telemetry.TrackException(new ResponseException(message));
                                    return onResponse(HttpStatusCode.Conflict, message, message);
                                });
                        },
                        () =>
                        {
                            var message = "Token is not connected to a user in this system";
                            telemetry.TrackException(new ResponseException(message));
                            return onResponse(HttpStatusCode.Conflict, message, message).ToTask();
                        });
                },
                async (why) =>
                {
                    var message = $"Invalid token:{why}";
                    var saveAuthLogAsync = await saveAuthLogTask;
                    await saveAuthLogAsync(false, message, values);
                    telemetry.TrackException(new ResponseException());
                    return onResponse(HttpStatusCode.BadRequest, message, $"Invalid token:{why}");
                },
                async (why) =>
                {
                    var message = $"Cannot create session because service is unavailable: {why}";
                    var saveAuthLogAsync = await saveAuthLogTask;
                    await saveAuthLogAsync(false, message, values);
                    telemetry.TrackException(new ResponseException(message));
                    return onResponse(HttpStatusCode.ServiceUnavailable, message, why);
                },
                async (why) =>
                {
                    var message = $"Cannot create session because service is unavailable: {why}";
                    var saveAuthLogAsync = await saveAuthLogTask;
                    await saveAuthLogAsync(false, message, values);
                    telemetry.TrackException(new ResponseException(message));
                    return onResponse(HttpStatusCode.ServiceUnavailable, message, why);
                },
                async (why) =>
                {
                    var message = $"General failure: {why}";
                    var saveAuthLogAsync = await saveAuthLogTask;
                    await saveAuthLogAsync(false, message, values);
                    telemetry.TrackException(new ResponseException(message));
                    return onResponse(HttpStatusCode.Conflict, message, why);
                });
            return response;
        }

        private static async Task<TResult> CreateResponse<TResult>(AzureApplication application, IProvideAuthorization authorizationProvider,
            string method, AuthenticationActions action,
            Guid sessionId, Guid? authorizationId, string jwtToken, string refreshToken,
            IDictionary<string, string> extraParams, Uri redirectUrl,
            Func<Uri, string, TResult> onRedirect,
            Func<HttpStatusCode, string, string, TResult> onResponse,
            TelemetryClient telemetry)
        {
            var redirectResponse = await application.GetRedirectUriAsync(authorizationProvider,
                    method, action,
                    sessionId, authorizationId, jwtToken, refreshToken, extraParams,
                    redirectUrl,
                (redirectUrlSelected) =>
                {
                    telemetry.TrackEvent($"CreateResponse - redirectUrlSelected1: {redirectUrlSelected.AbsolutePath}");
                    telemetry.TrackEvent($"CreateResponse - redirectUrlSelected2: {redirectUrlSelected.AbsoluteUri}");
                    return onRedirect(redirectUrlSelected, null);
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

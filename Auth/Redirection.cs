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
using EastFive.Api.Azure;

namespace EastFive.Azure.Auth
{
    public class Redirection
    {
        public static Task<TResult> ProcessRequestAsync<TResult>(
                string method, IDictionary<string, string> values,
                AzureApplication application, HttpRequestMessage request,
                System.Web.Http.Routing.UrlHelper urlHelper,
            Func<Uri, object, TResult> onRedirect,
            Func<string, TResult> onResponse)
        {
            var authorizationRequestManager = application.AuthorizationRequestManager;

            var telemetry = application.Telemetry;
            telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - Requesting credential manager.");

            var requestId = Guid.NewGuid();

            return authorizationRequestManager.CredentialValidation<TResult>(requestId, application,
                    method, values,
                () =>
                {
                    var baseUri = request.RequestUri;
                    return AuthenticationAsync(requestId, method, values, baseUri, application,
                        onRedirect,
                        onResponse);
                },
                (why) => onResponse(why));
        }

        public async static Task<TResult> AuthenticationAsync<TResult>(Guid requestId,
                string method, IDictionary<string, string> values, Uri baseUri,
                AzureApplication application,
            Func<Uri, object, TResult> onRedirect,
            Func<string, TResult> onResponse)
        {
            var context = application.AzureContext;
            var authorizationRequestManager = application.AuthorizationRequestManager;
            var telemetry = application.Telemetry;
            Func<string, TResult> onStop = (why) => onResponse(why);
            return await await context.Sessions.CreateOrUpdateWithAuthenticationAsync(
                    application, method, values,

            #region LOGIN
                (sessionId, authorizationId, token, refreshToken, action, provider, extraParams, redirectUrl) =>
                    authorizationRequestManager.CreatedAuthenticationLoginAsync(requestId, application, sessionId, authorizationId,
                            token, refreshToken, method, action, provider, extraParams, redirectUrl,
                        () => CreateResponse(application, provider, method, action, sessionId, authorizationId,
                                token, refreshToken, extraParams, baseUri, redirectUrl,
                            onRedirect,
                            onResponse,
                            telemetry),
                        onStop),
            #endregion

            #region LOGOUT
                (redirectUrl, reason, provider, extraParams) =>
                        authorizationRequestManager.CreatedAuthenticationLogoutAsync(requestId, application,
                                reason, method, provider, extraParams, redirectUrl,
                            async () =>
                            {
                                if (redirectUrl.IsDefaultOrNull())
                                    return Web.Configuration.Settings.GetUri(Security.SessionServer.Configuration.AppSettings.LandingPage,
                                            (redirect) => onRedirect(redirectUrl, reason),
                                            (why) => onResponse($"Location was null"));
                                if (redirectUrl.Query.IsNullOrWhiteSpace())
                                    redirectUrl = redirectUrl.SetQueryParam("cache", Guid.NewGuid().ToString("N"));
                                return await onRedirect(redirectUrl, reason).AsTask();
                            },
                            onStop),
            #endregion

            #region UNMAPPED USER
                async (subject, credentialProvider, extraParams, createMappingAsync) =>
                        authorizationRequestManager.CredentialUnmappedAsync<TResult>(requestId, application,
                                subject, method, credentialProvider, extraParams, createMappingAsync,
                            (createMappingNewAsync) => UnmappedCredentailAsync(application,
                                credentialProvider, method, subject, extraParams, baseUri,
                                createMappingNewAsync,
                                onRedirect,
                                onResponse,
                                telemetry),
                            onStop),
            #endregion

            #region Errors

                    async (why) =>
                    {
                        var message = $"Invalid token:{why}";
                        //await saveAuthLogAsync(false, message, values);
                        telemetry.TrackException(new ResponseException());
                        return onResponse($"Invalid token:{why}");
                    },
                    async (why) =>
                    {
                        var message = $"Cannot create session because service is unavailable: {why}";
                        //await saveAuthLogAsync(false, message, values);
                        telemetry.TrackException(new ResponseException(message));
                        return onResponse(why);
                    },
                    async (why) =>
                    {
                        var message = $"Cannot create session because service is unavailable: {why}";
                        //await saveAuthLogAsync(false, message, values);
                        telemetry.TrackException(new ResponseException(message));
                        return onResponse(why);
                    },
                    async (why) =>
                    {
                        var message = $"General failure: {why}";
                        //await saveAuthLogAsync(false, message, values);
                        telemetry.TrackException(new ResponseException(message));
                        return onResponse(why);
                    });

            #endregion
        }

        public static Task<TResult> CreateResponse<TResult>(AzureApplication application, IProvideAuthorization authorizationProvider,
            string method, AuthenticationActions action,
            Guid sessionId, Guid? authorizationId, string jwtToken, string refreshToken,
            IDictionary<string, string> extraParams, Uri baseUri, Uri redirectUrl,
            Func<Uri, object, TResult> onRedirect,
            Func<string, TResult> onBadResponse,
            TelemetryClient telemetry)
        {
            return application.GetRedirectUriAsync(authorizationProvider,
                    method, action,
                    sessionId, authorizationId, jwtToken, refreshToken, extraParams,
                    baseUri,
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
                    return onBadResponse(message);
                },
                (why) =>
                {
                    var message = $"General failure while completing login: {why}";
                    telemetry.TrackException(new ResponseException(message));
                    return onBadResponse(message);
                });
        }

        public static async Task<TResult> UnmappedCredentailAsync<TResult>(AzureApplication application,
                IProvideAuthorization authorizationProvider, string method, string subject, IDictionary<string, string> extraParams,
                Uri baseUri,
                Func<Guid,
                        Func<Guid, string, string, AuthenticationActions, Uri, Task<Task<TResult>>>,
                        Func<string, Task<TResult>>, Task<Task<TResult>>> createMappingAsync,
            Func<Uri, object, TResult> onRedirect,
            Func<string, TResult> onResponse,
            TelemetryClient telemetry)
        {
            return await await application.OnUnmappedUserAsync(method, authorizationProvider, subject, extraParams,
                async (authorizationId) =>
                {
                    //await updatingAuthLogTask;
                    telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - Creating Authentication.");
                    //updatingAuthLogTask = saveAuthLogAsync(true, $"New user mapping requested:{subject}/{credentialProvider.GetType().FullName}[{authorizationId}]", extraParams);
                    return await await createMappingAsync(authorizationId,
                        async (sessionId, jwtToken, refreshToken, action, redirectUrl) =>
                        {
                            //await updatingAuthLogTask;
                            //await saveAuthLogAsync(true, $"New user mapping requested:{subject}/{credentialProvider.GetType().FullName}[{authorizationId}]", extraParams);
                            telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - Created Authentication.  Creating response.");
                            var resp = CreateResponse(application, authorizationProvider, method, action,
                                    sessionId, authorizationId, jwtToken, refreshToken, extraParams,
                                    baseUri, redirectUrl,
                                onRedirect,
                                onResponse,
                                telemetry);
                            //await updatingAuthLogTask;
                            return resp;
                        },
                        async (why) =>
                        {
                            //await updatingAuthLogTask;
                            //await saveAuthLogAsync(true, $"Failure to create user mapping requested:{subject}/{credentialProvider.GetType().FullName}[{authorizationId}]: {why}", extraParams);
                            var message = $"Failure to connect token to a user in this system: {why}";
                            telemetry.TrackException(new ResponseException(message));
                            return onResponse(message);
                        });
                },
                () =>
                {
                    var message = "Token is not connected to a user in this system";
                    telemetry.TrackException(new ResponseException(message));
                    return onResponse(message).AsTask();
                });
        }
    }
}

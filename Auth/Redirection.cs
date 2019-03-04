//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net;
//using System.Net.Http;
//using System.Threading.Tasks;

//using BlackBarLabs;
//using EastFive;
//using BlackBarLabs.Api;
//using BlackBarLabs.Extensions;
//using EastFive.Collections.Generic;
//using EastFive.Security.SessionServer.Exceptions;
//using System.Web.Http;
//using Microsoft.ApplicationInsights;
//using EastFive.Extensions;
//using EastFive.Security.SessionServer;
//using EastFive.Api.Azure;

//namespace EastFive.Azure.Auth
//{
//    public class Redirection
//    {   
//        protected async static Task<HttpResponseMessage> ProcessRequestAsync<TResult>(AzureApplication application, string method, IDictionary<string, string> values,
//            HttpRequestMessage request, System.Web.Http.Routing.UrlHelper urlHelper)
//        {
//            var authorizationRequestManager = application.AuthorizationRequestManager;

//            var telemetry = application.Telemetry;
//            telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - Requesting credential manager.");
            
//            var requestId = Guid.NewGuid();

//            return await authorizationRequestManager.CredentialValidation<TResult>(requestId, application,
//                    method, values,
//                () => AuthenticationAsync(requestId, method, values, baseUri, application,
//                    onRedirect, onResponse),
//                (why) => onResponse(HttpStatusCode.ServiceUnavailable, why, why));
//        }

//        public async static Task<TResult> AuthenticationAsync<TResult>(Guid requestId, 
//                string method, IDictionary<string, string> values, Uri baseUri,
//                AzureApplication application, 
//            Func<Uri, string, TResult> onRedirect,
//            Func<HttpStatusCode, string, string, TResult> onResponse)
//        {
//            var context = application.AzureContext;
//            var authorizationRequestManager = application.AuthorizationRequestManager;
//            var telemetry = application.Telemetry;
//            Func<string, TResult> onStop = (why) => onResponse(HttpStatusCode.ServiceUnavailable, why, why);
//            return await await context.Sessions.CreateOrUpdateWithAuthenticationAsync(
//                    application, method, values,

//                #region LOGIN
//                (sessionId, authorizationId, token, refreshToken, action, provider, extraParams, redirectUrl) =>
//                    authorizationRequestManager.CreatedAuthenticationLoginAsync(requestId, application, sessionId, authorizationId,
//                            token, refreshToken, method, action, provider, extraParams, redirectUrl,
//                        () => CreateResponse(application, provider, method, action, sessionId, authorizationId,
//                                token, refreshToken, extraParams, baseUri, redirectUrl,
//                            onRedirect,
//                            onResponse,
//                            telemetry),
//                        onStop),
//                #endregion

//                #region LOGOUT
//                (redirectUrl, reason, provider, extraParams) =>
//                        authorizationRequestManager.CreatedAuthenticationLogoutAsync(requestId, application,
//                                reason, method, provider, extraParams, redirectUrl,
//                            async () =>
//                            {
//                                if (redirectUrl.IsDefaultOrNull())
//                                    return Web.Configuration.Settings.GetUri(Security.SessionServer.Configuration.AppSettings.LandingPage,
//                                            (redirect) => onRedirect(redirectUrl, reason),
//                                            (why) => onResponse(HttpStatusCode.BadRequest, why, $"Location was null"));
//                                if (redirectUrl.Query.IsNullOrWhiteSpace())
//                                    redirectUrl = redirectUrl.SetQueryParam("cache", Guid.NewGuid().ToString("N"));
//                                return await onRedirect(redirectUrl, reason).AsTask();
//                            },
//                            onStop),
//                #endregion

//                #region UNMAPPED USER
//                async (subject, credentialProvider, extraParams, createMappingAsync) =>
//                        authorizationRequestManager.CredentialUnmappedAsync<TResult>(requestId, application,
//                                subject, method, credentialProvider, extraParams, createMappingAsync,
//                            (createMappingNewAsync) => UnmappedCredentailAsync(application,
//                                credentialProvider, method, subject, extraParams, baseUri,
//                                createMappingNewAsync,
//                                onRedirect,
//                                onResponse,
//                                telemetry),
//                            onStop),
//                #endregion

//            #region Errors

//                    async (why) =>
//                    {
//                        var message = $"Invalid token:{why}";
//                        //await saveAuthLogAsync(false, message, values);
//                        telemetry.TrackException(new ResponseException());
//                        return onResponse(HttpStatusCode.BadRequest, message, $"Invalid token:{why}");
//                    },
//                    async (why) =>
//                    {
//                        var message = $"Cannot create session because service is unavailable: {why}";
//                        //await saveAuthLogAsync(false, message, values);
//                        telemetry.TrackException(new ResponseException(message));
//                        return onResponse(HttpStatusCode.ServiceUnavailable, message, why);
//                    },
//                    async (why) =>
//                    {
//                        var message = $"Cannot create session because service is unavailable: {why}";
//                        //await saveAuthLogAsync(false, message, values);
//                        telemetry.TrackException(new ResponseException(message));
//                        return onResponse(HttpStatusCode.ServiceUnavailable, message, why);
//                    },
//                    async (why) =>
//                    {
//                        var message = $"General failure: {why}";
//                        //await saveAuthLogAsync(false, message, values);
//                        telemetry.TrackException(new ResponseException(message));
//                        return onResponse(HttpStatusCode.Conflict, message, why);
//                    });

//            #endregion
//        }

//        public static async Task<HttpResponseMessage> CreateResponse<TResult>(AzureApplication application, IProvideAuthorization authorizationProvider,
//            string method, AuthenticationActions action,
//            Guid sessionId, Guid? authorizationId, string jwtToken, string refreshToken,
//            IDictionary<string, string> extraParams, Uri baseUri, Uri redirectUrl,
//            Func<Uri, object, HttpResponseMessage> onRedirect,
//            Func<string, HttpResponseMessage> onBadResponse,
//            TelemetryClient telemetry)
//        {
//            var redirectResponse = await application.GetRedirectUriAsync(authorizationProvider,
//                    method, action,
//                    sessionId, authorizationId, jwtToken, refreshToken, extraParams,
//                    baseUri,
//                    redirectUrl,
//                (redirectUrlSelected) =>
//                {
//                    application.GetAuthorizedSpa();
//                    telemetry.TrackEvent($"CreateResponse - redirectUrlSelected1: {redirectUrlSelected.AbsolutePath}");
//                    telemetry.TrackEvent($"CreateResponse - redirectUrlSelected2: {redirectUrlSelected.AbsoluteUri}");
//                    return onRedirect(redirectUrlSelected, null);
//                },
//                (paramName, why) =>
//                {
//                    var message = $"Invalid parameter while completing login: {paramName} - {why}";
//                    telemetry.TrackException(new ResponseException(message));
//                    return onResponse(HttpStatusCode.BadRequest, message, why);
//                },
//                (why) =>
//                {
//                    var message = $"General failure while completing login: {why}";
//                    telemetry.TrackException(new ResponseException(message));
//                    return onResponse(HttpStatusCode.BadRequest, message, why);
//                });

//            var msg = redirectResponse;
//            telemetry.TrackEvent($"CreateResponse - {msg}");
//            return redirectResponse;
//        }

//        public static async Task<TResult> UnmappedCredentailAsync<TResult>(AzureApplication application,
//                IProvideAuthorization authorizationProvider, string method, string subject, IDictionary<string, string> extraParams,
//                Uri baseUri,
//                Func<Guid,
//                        Func<Guid, string, string, AuthenticationActions, Uri, Task<Task<TResult>>>,
//                        Func<string, Task<TResult>>, Task<Task<TResult>>> createMappingAsync,
//            Func<Uri, string, TResult> onRedirect,
//            Func<HttpStatusCode, string, string, TResult> onResponse,
//            TelemetryClient telemetry)
//        {
//            return await await application.OnUnmappedUserAsync<Task<TResult>>(method, authorizationProvider, subject, extraParams,
//                async (authorizationId) =>
//                {
//                    //await updatingAuthLogTask;
//                    telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - Creating Authentication.");
//                    //updatingAuthLogTask = saveAuthLogAsync(true, $"New user mapping requested:{subject}/{credentialProvider.GetType().FullName}[{authorizationId}]", extraParams);
//                    return await await createMappingAsync(authorizationId,
//                        async (sessionId, jwtToken, refreshToken, action, redirectUrl) =>
//                        {
//                            //await updatingAuthLogTask;
//                            //await saveAuthLogAsync(true, $"New user mapping requested:{subject}/{credentialProvider.GetType().FullName}[{authorizationId}]", extraParams);
//                            telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - Created Authentication.  Creating response.");
//                            var resp = CreateResponse(application, authorizationProvider, method, action, 
//                                    sessionId, authorizationId, jwtToken, refreshToken, extraParams, 
//                                    baseUri, redirectUrl,
//                                onRedirect, 
//                                onResponse,
//                                telemetry);
//                            //await updatingAuthLogTask;
//                            return resp;
//                        },
//                        async (why) =>
//                        {
//                            //await updatingAuthLogTask;
//                            //await saveAuthLogAsync(true, $"Failure to create user mapping requested:{subject}/{credentialProvider.GetType().FullName}[{authorizationId}]: {why}", extraParams);
//                            var message = $"Failure to connect token to a user in this system: {why}";
//                            telemetry.TrackException(new ResponseException(message));
//                            return onResponse(HttpStatusCode.Conflict, message, message);
//                        });
//                },
//                () =>
//                {
//                    var message = "Token is not connected to a user in this system";
//                    telemetry.TrackException(new ResponseException(message));
//                    return onResponse(HttpStatusCode.Conflict, message, message).ToTask();
//                });
//        }
//    }
//}

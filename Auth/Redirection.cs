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
using EastFive.Azure.Persistence.AzureStorageTables;

namespace EastFive.Azure.Auth
{
    public class Redirection
    {

        public static Task<TResult> ProcessRequestAsync<TResult>(
                EastFive.Azure.Auth.Method authentication, 
                IDictionary<string, string> values,
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
                    authentication.authenticationId, values,
                () =>
                {
                    var baseUri = request.RequestUri;
                    return AuthenticationAsync(requestId, authentication, values, baseUri, application,
                        onRedirect,
                        onResponse,
                        onResponse,
                        () => onResponse("Authorization not found"));
                },
                (why) => onResponse(why));
        }

        public async static Task<TResult> AuthenticationAsync<TResult>(Guid requestId,
                EastFive.Azure.Auth.Method authentication, IDictionary<string, string> values, Uri baseUri,
                AzureApplication application,
            Func<Uri, object, TResult> onRedirect,
            Func<string, TResult> onResponse,
            Func<string, TResult> onBadResponse,
            Func<TResult> onAuthorizationNotFound)
        {
            var authorizationRequestManager = application.AuthorizationRequestManager;
            var telemetry = application.Telemetry;
            return await await authentication.RedeemTokenAsync(values, application,
                async (externalAccountKey, authorizationRefMaybe, loginProvider, extraParams) =>
                {
                    if (!authorizationRefMaybe.HasValue)
                    {
                        var authorization = new Authorization
                        {
                            authorizationRef = new Ref<Authorization>(Security.SecureGuid.Generate()),
                            Method = authentication.authenticationId,
                            parameters = extraParams,
                        };
                        return await ProcessAsync(authorization,
                                async (authorizationUpdated) =>
                                {
                                    bool created = await authorizationUpdated.StorageCreateAsync(
                                        authIdDiscard =>
                                        {
                                            return true;
                                        },
                                        () => throw new Exception("Secure guid not unique"));
                                },
                                authentication, externalAccountKey, extraParams,
                                requestId, baseUri, application, loginProvider,
                            onRedirect,
                            onResponse,
                            onBadResponse,
                                telemetry);
                    }
                    var authorizationRef = authorizationRefMaybe.Ref;
                    return await authorizationRef.StorageUpdateAsync(
                        async (authorization, saveAsync) =>
                        {
                            return await ProcessAsync(authorization, saveAsync,
                                authentication, externalAccountKey, extraParams,
                                requestId, baseUri, application, loginProvider,
                                onRedirect,
                                onResponse,
                                onBadResponse,
                                    telemetry);
                        },
                        () =>
                        {
                            return onAuthorizationNotFound();
                        });
                },
                (authorizationRef, extraparams) => throw new NotImplementedException(),
                (why) => onBadResponse(why).AsTask());
        }

        public static async Task<TResult> ProcessAsync<TResult>(Authorization authorization,
                Func<Authorization, Task> saveAsync,
                Method authentication, string externalAccountKey,
                IDictionary<string, string> extraParams,
                Guid requestId, Uri baseUri, AzureApplication application, IProvideLogin loginProvider,
            Func<Uri, object, TResult> onRedirect,
            Func<string, TResult> onResponse,
            Func<string, TResult> onBadResponse,
            TelemetryClient telemetry)
        {
            return await await AccountMapping.FindByMethodAndKeyAsync(authentication.authenticationId, externalAccountKey,
                    authorization,
                async internalAccountId =>
                {
                    authorization.parameters = extraParams;
                    await saveAsync(authorization);
                    return await CreateLoginResponse(requestId,
                                internalAccountId, extraParams,
                                authentication, authorization,
                                baseUri,
                                application, loginProvider,
                            onRedirect,
                            onBadResponse,
                            telemetry);
                },
                async () =>
                {
                        return await await UnmappedCredentialAsync(externalAccountKey, extraParams,
                            authentication, authorization,
                            loginProvider, application, baseUri,

                            // Create mapping
                            async (internalAccountId) =>
                            {
                                authorization.parameters = extraParams;
                                await saveAsync(authorization);
                                return await AccountMapping.CreateByMethodAndKeyAsync(authorization, externalAccountKey,
                                    internalAccountId,
                                    () =>
                                    {
                                        return CreateLoginResponse(requestId,
                                                internalAccountId, extraParams,
                                                authentication, authorization,
                                                baseUri,
                                                application, loginProvider,
                                            onRedirect,
                                            onBadResponse,
                                            telemetry);
                                    },
                                    (why) => onResponse(why).AsTask());
                            },
                            async (why) =>
                            {
                                            // Save params so they can be used later
                                            authorization.parameters = extraParams;
                                await saveAsync(authorization);
                                return onResponse(why);
                            },
                            async (interceptionUrl) =>
                            {
                                authorization.parameters = extraParams;
                                await saveAsync(authorization);
                                return onRedirect(interceptionUrl, null);
                            },
                            telemetry);
                });
        }

        public static async Task<TResult> MapAccountAsync<TResult>(Authorization authorization,
            Guid internalAccountId, string externalAccountKey,
            Guid requestId,
            Uri baseUri,
            AzureApplication application,
            Func<Uri, TResult> onRedirect,
            Func<string, TResult> onFailure,
            TelemetryClient telemetry)
        {
            return await await AccountMapping.CreateByMethodAndKeyAsync(authorization, externalAccountKey,
                internalAccountId,
                async () =>
                {
                    return await await Method.ById(authorization.Method, application,
                        async method =>
                        {
                            return await await method.GetLoginProviderAsync(application,
                                (loginProviderMethodName, loginProvider) =>
                                {
                                    return CreateLoginResponse(requestId,
                                           internalAccountId, authorization.parameters,
                                           method, authorization,
                                           baseUri,
                                           application, loginProvider,
                                       (url, obj) => onRedirect(url),
                                       onFailure,
                                       telemetry);
                                },
                                () =>
                                {
                                    return onFailure("Login provider is no longer supported by the system").AsTask();
                                });
                        },
                        () => onFailure("Method no longer suppored.").AsTask());
                },
                (why) => onFailure(why).AsTask());
        }

        public static Task<TResult> CreateLoginResponse<TResult>(Guid requestId,
                Guid accountId, IDictionary<string, string> extraParams,
                Method method, Authorization authorization,
                Uri baseUri,
                AzureApplication application, IProvideAuthorization authorizationProvider,
            Func<Uri, object, TResult> onRedirect,
            Func<string, TResult> onBadResponse,
            TelemetryClient telemetry)
        {
            return application.GetRedirectUriAsync(requestId, 
                    accountId, extraParams,
                    method, authorization,
                    baseUri,
                    authorizationProvider,
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

        public static async Task<TResult> UnmappedCredentialAsync<TResult>(
                string subject, IDictionary<string, string> extraParams,
                EastFive.Azure.Auth.Method authentication, Authorization authorization,
                IProvideAuthorization authorizationProvider, 
                AzureApplication application,
                Uri baseUri,
            Func<Guid,Task<TResult>> createMappingAsync,
            Func<string, TResult> onResponse,
            Func<Uri, TResult> onInterceptProcess,
            TelemetryClient telemetry)
        {
            return await await application.OnUnmappedUserAsync(subject, extraParams,
                    authentication, authorization, authorizationProvider, baseUri,
                (accountId) => createMappingAsync(accountId),
                (callback) => onInterceptProcess(callback).AsTask(),
                () =>
                {
                    var message = "Token is not connected to a user in this system";
                    telemetry.TrackException(new ResponseException(message));
                    return onResponse(message).AsTask();
                });
        }
    }
}

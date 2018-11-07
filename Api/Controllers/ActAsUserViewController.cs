using BlackBarLabs.Extensions;
using EastFive.Api;
using EastFive.Api.Azure.Credentials;
using EastFive.Api.Azure.Credentials.Controllers;
using EastFive.Api.Controllers;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    [FunctionViewController(Resource = typeof(CredentialProcessDocument), Route = "SessionManagement")]
    public class ActAsUserViewController : Controller
    {
        public async Task<ActionResult> Index(string redirectUri, string token)
        {
            return View("~/Views/ActAsUser/");
        }

        [EastFive.Api.HttpGet (MatchAllParameters = true )]
        public static async Task<HttpResponseMessage> SessionManagement(
            //EastFive.Api.Controllers.Security security,
            EastFive.Api.Azure.AzureApplication application,
            UnauthorizedResponse onUnauthorized,
            ViewFileResponse viewResponse)
        {
            //if (!await application.IsAdminAsync(security))
            //    return onUnauthorized();
            return await CredentialProcessDocument.FindAllAsync(
                (documents) =>
                {
                    var orderedDocs = documents.OrderByDescending(doc => doc.Time).ToArray();
                    return viewResponse("/SessionManagement/Index.cshtml", orderedDocs);
                },
                BlackBarLabs.Persistence.Azure.StorageTables.AzureStorageRepository.CreateRepository(
                    EastFive.Azure.AppSettings.ASTConnectionStringKey));
        }
        
        [EastFive.Api.HttpGet]
        public static async Task<HttpResponseMessage> ReplicateLogin(
            [QueryParameter(Name = "credential_process_id")]Guid credentialProcessId,
            EastFive.Api.Azure.AzureApplication application, HttpRequestMessage request,
            EastFive.Api.Controllers.RedirectResponse redirectResponse,
            EastFive.Api.Controllers.ViewStringResponse viewResponse)
        {
            return await await CredentialProcessDocument.FindByIdAsync(credentialProcessId,
                (document) =>
                {
                    return ResponseController.ProcessRequestAsync(application, document.Method, request.RequestUri, document.GetValuesCredential(),
                        (redirectUri, message) => redirectResponse(redirectUri, message),
                        (code, message, reason) => viewResponse($"<html><head><title>{reason}</title></head><body>{message}</body></html>", null));
                },
                () => viewResponse("", null).ToTask(),
                BlackBarLabs.Persistence.Azure.StorageTables.AzureStorageRepository.CreateRepository(
                    EastFive.Azure.AppSettings.ASTConnectionStringKey));
        }

        [EastFive.Api.HttpGet]
        public static async Task<HttpResponseMessage> AuthenticationAsync(
            [QueryParameter(Name = "authentication_process_id")]Guid credentialProcessId,
            EastFive.Api.Azure.AzureApplication application, HttpRequestMessage request,
            EastFive.Api.Controllers.RedirectResponse redirectResponse,
            EastFive.Api.Controllers.ViewStringResponse viewResponse)
        {
            return await await CredentialProcessDocument.FindByIdAsync(credentialProcessId,
                (document) =>
                {
                    return ResponseController.AuthenticationAsync(credentialProcessId,
                            document.Method, document.GetValuesCredential(), request.RequestUri,
                            application,
                        (redirectUri, message) => redirectResponse(redirectUri, message),
                        (code, message, reason) => viewResponse($"<html><head><title>{reason}</title></head><body>{message}</body></html>", null));
                },
                () => viewResponse("", null).ToTask(),
                BlackBarLabs.Persistence.Azure.StorageTables.AzureStorageRepository.CreateRepository(
                    EastFive.Azure.AppSettings.ASTConnectionStringKey));
        }

        [EastFive.Api.HttpGet]
        public static async Task<HttpResponseMessage> RedeemAsync(
            [QueryParameter(Name = "redemption_process_id")]Guid credentialProcessId,
            EastFive.Api.Azure.AzureApplication application, HttpRequestMessage request,
            EastFive.Api.Controllers.RedirectResponse redirectResponse,
            EastFive.Api.Controllers.ViewStringResponse viewResponse)
        {
            return await await CredentialProcessDocument.FindByIdAsync(credentialProcessId,
                (document) =>
                {
                    var context = application.AzureContext;
                    var provider = application.AuthorizationProviders.First(prov => prov.Value.GetType().FullName == document.Provider).Value;
                    // Enum.TryParse(document.Action, out AuthenticationActions action);
                    var responseParameters = document.GetValuesCredential();
                    return provider.ParseCredentailParameters<Task<HttpResponseMessage>>(responseParameters,
                        async (subject, stateId, loginId) => await await context.Sessions.TokenRedeemedAsync<Task<HttpResponseMessage>>(
                            document.Method, provider, subject, stateId, loginId, responseParameters,
                            (sessionId, authorizationId, token, refreshToken, actionReturned, providerReturned, extraParams, redirectUrl) =>
                                ResponseController.CreateResponse(application, providerReturned, document.Method, actionReturned, sessionId, authorizationId,
                                        token, refreshToken, extraParams, request.RequestUri, redirectUrl,
                                    (redirectUri, message) => redirectResponse(redirectUri, message),
                                    (code, message, reason) => viewResponse($"<html><head><title>{reason}</title></head><body>{message}</body></html>", null),
                                    application.Telemetry),
                            async (redirectUrl, reason, providerReturned, extraParams) =>
                            {
                                if (redirectUrl.IsDefaultOrNull())
                                    return Web.Configuration.Settings.GetUri(Security.SessionServer.Configuration.AppSettings.LandingPage,
                                            (redirect) => redirectResponse(redirectUrl, reason),
                                            (why) => viewResponse($"<html><head><title>{reason}</title></head><body>{why}</body></html>", null));
                                if (redirectUrl.Query.IsNullOrWhiteSpace())
                                    redirectUrl = redirectUrl.SetQueryParam("cache", Guid.NewGuid().ToString("N"));
                                return await redirectResponse(redirectUrl, reason).ToTask();
                            },
                            (subjectReturned, credentialProvider, extraParams, createMappingAsync) =>
                                ResponseController.UnmappedCredentailAsync(application,
                                        credentialProvider, document.Method, subjectReturned, extraParams, request.RequestUri,
                                        createMappingAsync,
                                    (redirectUri, message) => redirectResponse(redirectUri, message),
                                    (code, message, reason) => viewResponse($"<html><head><title>{reason}</title></head><body>{message}</body></html>", null),
                                    application.Telemetry).ToTask(),
                            (why) => viewResponse($"<html><head><title>{why}</title></head><body>{why}</body></html>", null).ToTask(),
                            (why) => viewResponse($"<html><head><title>{why}</title></head><body>{why}</body></html>", null).ToTask(),
                            (why) => viewResponse($"<html><head><title>{why}</title></head><body>{why}</body></html>", null).ToTask(),
                            application.Telemetry),
                        (why) => viewResponse($"<html><head><title>{why}</title></head><body>{why}</body></html>", null).ToTask());
                },
                () => viewResponse("", null).ToTask(),
                BlackBarLabs.Persistence.Azure.StorageTables.AzureStorageRepository.CreateRepository(
                    EastFive.Azure.AppSettings.ASTConnectionStringKey));
        }

        [EastFive.Api.HttpGet]
        public static async Task<HttpResponseMessage> CreateResponseAsync(
            [QueryParameter(Name = "login_process_id")]Guid credentialProcessId,
            EastFive.Api.Azure.AzureApplication application, HttpRequestMessage request,
            EastFive.Api.Controllers.RedirectResponse redirectResponse,
            EastFive.Api.Controllers.ViewStringResponse viewResponse)
        {
            return await await CredentialProcessDocument.FindByIdAsync(credentialProcessId,
                (document) =>
                {
                    var provider = application.AuthorizationProviders.First(prov => prov.Value.GetType().FullName == document.Provider).Value;
                    Enum.TryParse(document.Action, out AuthenticationActions action);
                    return ResponseController.CreateResponse(application, provider, document.Method, action,
                            document.SessionId, document.AuthorizationId, document.Token, document.RefreshToken,
                            document.GetValuesCredential(), request.RequestUri, 
                            document.RedirectUrl.IsNullOrWhiteSpace(
                                () => null,
                                redirUrlString => new Uri(redirUrlString)),
                        (redirectUri, message) => redirectResponse(redirectUri, message),
                        (code, message, reason) => viewResponse($"<html><head><title>{reason}</title></head><body>{message}</body></html>", null),
                        application.Telemetry);
                },
                () => viewResponse("", null).ToTask(),
                BlackBarLabs.Persistence.Azure.StorageTables.AzureStorageRepository.CreateRepository(
                    EastFive.Azure.AppSettings.ASTConnectionStringKey));
        }

    }
    
}

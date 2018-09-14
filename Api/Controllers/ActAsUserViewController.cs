using BlackBarLabs.Extensions;
using EastFive.Api;
using EastFive.Api.Azure.Credentials;
using EastFive.Api.Azure.Credentials.Controllers;
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

        [EastFive.Api.HttpGet]
        public static Task<HttpResponseMessage> SessionManagement(
            EastFive.Api.Controllers.ViewFileResponse viewResponse)
        {
            return CredentialProcessDocument.FindAllAsync(
                (documents) =>
                {
                    var orderedDocs = documents.OrderByDescending(doc => doc.Time).ToArray();
                    return viewResponse("/SessionManagement/Index.cshtml", orderedDocs);
                },
                BlackBarLabs.Persistence.Azure.StorageTables.AzureStorageRepository.CreateRepository(
                    Configuration.AppSettings.Storage));
        }


        [EastFive.Api.HttpGet]
        public static async Task<HttpResponseMessage> ReplicateLogin(
            [QueryValidation(Name = "credential_process_id")]Guid credentialProcessId,
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
                    Configuration.AppSettings.Storage));
        }

        [EastFive.Api.HttpGet]
        public static async Task<HttpResponseMessage> AuthenticationAsync(
            [QueryValidation(Name = "authentication_process_id")]Guid credentialProcessId,
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
                    Configuration.AppSettings.Storage));
        }

        [EastFive.Api.HttpGet]
        public static async Task<HttpResponseMessage> CreateResponseAsync(
            [QueryValidation(Name = "login_process_id")]Guid credentialProcessId,
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
                    Configuration.AppSettings.Storage));
        }

    }
    
}

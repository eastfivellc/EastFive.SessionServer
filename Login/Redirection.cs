using BlackBarLabs.Api;
using EastFive.Api;
using EastFive.Api.Azure;
using EastFive.Api.Controllers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Routing;

namespace EastFive.Azure.Login
{
    [FunctionViewController4(
        Route = "Redirection",
        Resource = typeof(Redirection),
        ContentType = "x-application/login-redirection",
        ContentTypeVersion = "0.1")]
    public class Redirection
    {
        [HttpGet(MatchAllParameters = false)]
        public static async Task<HttpResponseMessage> Get(
                AzureApplication application, UrlHelper urlHelper,
                HttpRequestMessage request,
            RedirectResponse onRedirectResponse,
            ServiceUnavailableResponse onNoServiceResponse,
            BadRequestResponse onBadCredentials,
            GeneralConflictResponse onFailure)
        {
            var parameters = request.RequestUri.ParseQuery();
            var authentication = await EastFive.Azure.Auth.Method.ByMethodName(
                CredentialProvider.IntegrationName, application);

            return await EastFive.Azure.Auth.Redirection.ProcessRequestAsync(authentication,
                    parameters,
                    application,
                    request, urlHelper,
                (redirect) => onRedirectResponse(redirect, "success"),
                (why) => onBadCredentials().AddReason($"Bad credentials:{why}"),
                (why) => onNoServiceResponse().AddReason(why),
                (why) => onFailure(why));
        }

    }
}

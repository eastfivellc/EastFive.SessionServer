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
using EastFive.Extensions;
using EastFive.Security.SessionServer.Api.Controllers;
using EastFive.Collections.Generic;

namespace EastFive.Api.Azure.Controllers
{
    [FunctionViewController(Route = "InternalIntegrationResponse")]
    public class InternalIntegrationResponseController : ResponseController
    {
        public override async Task<IHttpActionResult> Get([FromUri]ResponseResult query)
        {
            var kvps = Request.GetQueryNameValuePairs();
            var action = await ProcessRequestAsync(Security.CredentialProvider.InternalProvider.IntegrationName, kvps.ToDictionary(),
                (location) => Redirect(location),
                (code, body, reason) =>
                    this.Request
                        .CreateResponse(code, body)
                        .AddReason(reason)
                        .ToActionResult());
            return action;
        }

        [HttpGet]
        public static async Task<HttpResponseMessage> GetResponse([QueryDefaultParameter][Required]Guid integrationId, [Required]string resourceTypes,
            HttpRequestMessage request, Api.Controllers.RedirectResponse onRedirect)
        {
            var extraParams = new Dictionary<string, string>()
            {
                { Security.CredentialProvider.InternalProvider.integrationIdKey, integrationId.ToString() },
                { Security.CredentialProvider.InternalProvider.resourceTypes, resourceTypes },
            };
            return await ProcessRequestAsync(Security.CredentialProvider.InternalProvider.IntegrationName, extraParams,
                (location) => onRedirect(location),
                (code, body, reason) =>
                    request
                        .CreateResponse(code, body)
                        .AddReason(reason));
            
        }
    }
}
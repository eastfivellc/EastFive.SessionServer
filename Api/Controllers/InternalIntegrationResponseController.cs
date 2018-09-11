﻿using System;
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
using EastFive.Api.Controllers;

namespace EastFive.Api.Azure.Credentials.Controllers
{
    [FunctionViewController(Route = "InternalIntegrationResponse")]
    public static class InternalIntegrationResponseController
    {
        [HttpGet]
        public static async Task<HttpResponseMessage> GetResponse(AzureApplication application, 
            [QueryDefaultParameter][Required]Guid integrationId, [Required]string resourceTypes,
            HttpRequestMessage request, RedirectResponse onRedirect)
        {
            var extraParams = new Dictionary<string, string>()
            {
                { InternalProvider.integrationIdKey, integrationId.ToString() },
                { InternalProvider.resourceTypes, resourceTypes },
            };
            return await ResponseController.ProcessRequestAsync(application, InternalProvider.IntegrationName, request.RequestUri, extraParams,
                (location, why) => onRedirect(location, why),
                (code, body, reason) =>
                    request
                        .CreateResponse(code, body)
                        .AddReason(reason));
            
        }
    }
}
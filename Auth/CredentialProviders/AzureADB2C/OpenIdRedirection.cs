﻿using BlackBarLabs.Api;
using EastFive.Api;
using EastFive.Api.Azure;
using EastFive.Api.Azure.Credentials;
using EastFive.Api.Controllers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Routing;

namespace EastFive.Azure.Auth.CredentialProviders.AzureADB2C
{
    [FunctionViewController6(
        Route = "OpenIdRedirection",
        Resource = typeof(Redirection),
        ContentType = "x-application/auth-redirection.aadb2c",
        ContentTypeVersion = "0.1")]
    public class OpenIdRedirection : EastFive.Azure.Auth.Redirection
    {

        public const string id_token = "id_token";

        public const string state = "state";

        public string error { get; set; }

        public string error_description { get; set; }

        //[ApiProperty(PropertyName = ProvideLoginMock.extraParamState)]
        //[JsonProperty(PropertyName = ProvideLoginMock.extraParamState)]
        //public Guid? state;

        //[ApiProperty(PropertyName = ProvideLoginMock.extraParamToken)]
        //[JsonProperty(PropertyName = ProvideLoginMock.extraParamToken)]
        //public string token;

        [HttpGet(MatchAllParameters = false)]
        public static async Task<HttpResponseMessage> Get(
                //[QueryParameter(Name = ProvideLoginMock.extraParamState)]IRefOptional<Authorization> authorizationRef,
                //[QueryParameter(Name = ProvideLoginMock.extraParamToken)]string token,
                AzureApplication application, UrlHelper urlHelper,
                HttpRequestMessage request,
            RedirectResponse onRedirectResponse,
            ServiceUnavailableResponse onNoServiceResponse,
            BadRequestResponse onBadCredentials,
            GeneralConflictResponse onFailure)
        {
            var parameters = request.RequestUri.ParseQuery();
            var authentication = await EastFive.Azure.Auth.Method.ByMethodName(
                AzureADB2CProvider.IntegrationName, application);
            
            return await EastFive.Azure.Auth.Redirection.ProcessRequestAsync(authentication, 
                    parameters,
                    application,
                    request, urlHelper,
                (redirect) => onRedirectResponse(redirect),
                (why) => onBadCredentials().AddReason($"Bad credentials:{why}"),
                (why) => onNoServiceResponse().AddReason(why),
                (why) => onFailure(why));
        }

        [HttpPost(MatchAllParameters = false)]
        public static async Task<HttpResponseMessage> PostAsync(
                [Property(Name = id_token)]string idToken,
                [Property(Name = state)]IRef<Authorization> authorization,
                AzureApplication application, UrlHelper urlHelper,
                HttpRequestMessage request,
            RedirectResponse onRedirectResponse,
            ServiceUnavailableResponse onNoServiceResponse,
            BadRequestResponse onBadCredentials,
            GeneralConflictResponse onFailure)
        {
            var parameters = new Dictionary<string, string>
            {
                { id_token, idToken },
                { state, authorization.id.ToString("N") },
            };
            var authentication = await EastFive.Azure.Auth.Method.ByMethodName(
                AzureADB2CProvider.IntegrationName, application);

            return await EastFive.Azure.Auth.Redirection.ProcessRequestAsync(authentication,
                    parameters,
                    application,
                    request, urlHelper,
                (redirect) => onRedirectResponse(redirect),
                (why) => onBadCredentials().AddReason($"Bad credentials:{why}"),
                (why) => onNoServiceResponse().AddReason(why),
                (why) => onFailure(why));
        }
    }
}

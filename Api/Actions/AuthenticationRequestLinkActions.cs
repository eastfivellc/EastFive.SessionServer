using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Net.Http;
using System.Web.Http.Routing;
using System.Configuration;

using BlackBarLabs;
using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using EastFive.Api.Services;
using EastFive.Security.SessionServer.Configuration;

namespace EastFive.Security.SessionServer.Api
{
    public static class AuthenticationRequestLinkActions
    {
        public static Task<HttpResponseMessage> QueryAsync(this Resources.Queries.AuthenticationRequestLinkQuery query,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return query.ParseAsync(request,
                    q => QueryByIdAsync(request, urlHelper),
                    q => QueryByIntegrationAsync(q.SupportsIntegration.ParamValue(), request, urlHelper));
        }

        private static async Task<HttpResponseMessage> QueryByIdAsync(
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.LoginProviders.GetAllAsync(
                (methods) =>
                {
                    var response = request.CreateResponse(HttpStatusCode.OK,
                        methods.Select(method => Convert(method, urlHelper)));
                    return response;
                },
                why => request.CreateResponse(HttpStatusCode.ServiceUnavailable).AddReason(why));
        }
        
        private static async Task<HttpResponseMessage> QueryByIntegrationAsync(bool supportsIntegrations,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.LoginProviders.GetAllAsync(supportsIntegrations,
                (methods) =>
                {
                    var response = request.CreateResponse(HttpStatusCode.OK,
                        methods.Select(method => Convert(method, urlHelper)));
                    return response;
                },
                why => request.CreateResponse(HttpStatusCode.ServiceUnavailable).AddReason(why));
        }

        private static Resources.AuthenticationRequestLink Convert(CredentialValidationMethodTypes method, UrlHelper urlHelper)
        {
            return new Resources.AuthenticationRequestLink
            {
                Id = urlHelper.GetWebId<Controllers.SessionController>(SecureGuid.Generate()),
                Method = method,
                Name = method.ToString(),
                SecureId = SecureGuid.Generate(),
            };
        }
    }
}

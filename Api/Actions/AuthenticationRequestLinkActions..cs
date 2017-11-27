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
                    q => QueryByIdAsync(request, urlHelper));
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
                });
        }

        private static Resources.AuthenticationRequestLink Convert(CredentialValidationMethodTypes method, UrlHelper urlHelper)
        {
            return new Resources.AuthenticationRequestLink
            {
                Id = urlHelper.GetWebId<Controllers.AuthenticationRequestController>(SecureGuid.Generate()),
                Link = urlHelper.GetLocation<Controllers.AuthenticationRequestController>(),
                Method = method,
                Name = method.ToString(),
                SessionId = SecureGuid.Generate(),
            };
        }
    }
}

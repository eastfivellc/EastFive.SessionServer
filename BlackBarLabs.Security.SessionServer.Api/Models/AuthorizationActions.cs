using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;

using BlackBarLabs.Api;
using BlackBarLabs.Extensions;

namespace BlackBarLabs.Security.SessionServer
{
    public static class AuthorizationActions
    {
        public static Task<HttpResponseMessage> QueryAsync(this Api.Resources.Authorization resource,
            HttpRequestMessage request)
        {
            return request.CreateResponse(HttpStatusCode.NotImplemented)
                .AddReason("Querying Authorization Resource is not yet supported")
                .ToTask();
        }

        public static async Task<HttpResponseMessage> OptionsAsync(this 
            HttpRequestMessage request)
        {
            var viewModel = new Api.Resources.Authorization
            {
                Id = Guid.NewGuid(),
                CredentialProviders = new Uri[] { new Uri("http://example.com/Credentials?UserId=" + Guid.NewGuid().ToString()) },
            };
            var response = new BlackBarLabs.Api.Resources.Options()
            {
                Post = new[] { viewModel },
            };

            var responseMessage = request.CreateResponse(System.Net.HttpStatusCode.OK, response);
            return await responseMessage.ToTask();
        }

        public static async Task<HttpResponseMessage> CreateAsync(this Api.Resources.Authorization resource,
            HttpRequestMessage request)
        {
            var context = request.GetSessionServerContext();
            var response = await context.Authorizations.CreateAsync(resource.Id.UUID,
                () => request.CreateResponse(HttpStatusCode.Created),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason("Authorization already exists"));
            return response;
        }
    }
}

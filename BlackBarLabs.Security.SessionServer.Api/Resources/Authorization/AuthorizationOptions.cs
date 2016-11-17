using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

using BlackBarLabs.Security.Authorization;

namespace BlackBarLabs.Security.AuthorizationServer.API.Resources
{
    public class AuthorizationOptions : Resource, IHttpActionResult
    {
        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            
            var viewModel = new Authorization
            {
                Id = Guid.NewGuid(),
                CredentialProviders = new Uri[] { new Uri("http://example.com/Credentials?UserId=" + Guid.NewGuid().ToString()) },
            };
            var response = new BlackBarLabs.Api.Resources.Options()
            {
                Post = new[] { viewModel },
            };

            var responseMessage = this.Request.CreateResponse(System.Net.HttpStatusCode.OK, response);
            return Task.FromResult(responseMessage);
        }
    }
}
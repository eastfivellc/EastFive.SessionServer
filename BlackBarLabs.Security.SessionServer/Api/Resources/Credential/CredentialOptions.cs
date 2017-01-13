using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

using BlackBarLabs.Security.Session;

namespace BlackBarLabs.Security.SessionServer.Api.Resources
{
    public class CredentialOptions : Resource, IHttpActionResult
    {
        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var credentialProviders = new Resources.Credential[]
            {
                new Resources.Credential
                {
                    Method = CredentialValidationMethodTypes.Facebook,
                    Provider = new Uri("http://api.facebook.com/Authorization"),
                    UserId = "0123456789",
                    Token = "ABC.123.MXC",
                },
                new Resources.Credential
                {
                    Method = CredentialValidationMethodTypes.OpenIdConnect,
                    Provider = new Uri("urn:auth.gibbits.nc2media.com/AuthOpenIdConnect/"),
                    UserId = Guid.NewGuid().ToString("N"),
                    Token = "EDF.123.A3EF",
                },
                new Resources.Credential
                {
                    Method = CredentialValidationMethodTypes.Implicit,
                    Provider = new Uri("http://www.example.com/ImplicitAuth"),
                    UserId = Guid.NewGuid().ToString("N"),
                    Token = Guid.NewGuid().ToString("N"),
                }
            };
            var response = new BlackBarLabs.Api.Resources.Options()
            {
                Get = credentialProviders,
            };

            var responseMessage = this.Request.CreateResponse(System.Net.HttpStatusCode.OK, response);
            return Task.FromResult(responseMessage);
        }
    }
}
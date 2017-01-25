using System;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http;
using System.Net.Http;
using System.Threading;

using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using System.Web.Http.Routing;
using BlackBarLabs;
using EastFive.Api.Services;

namespace EastFive.Security.SessionServer.Api
{
    public static class PasswordCredentialActions
    {
        public static async Task<HttpResponseMessage> CreateAsync(this Resources.PasswordCredential credential,
            HttpRequestMessage request, UrlHelper url)
        {
            var actorId = credential.Actor.ToGuid();
            //return await request.GetClaims(
            //    async (claims) =>
            //    {
            var claims = new System.Security.Claims.Claim[] { };
            var context = request.GetSessionServerContext();
            var creationResults = await context.CredentialMappings.CreatePasswordCredentialsAsync(
                credential.Id.UUID, actorId.Value,
                credential.UserId, credential.IsEmail, credential.Token, credential.ForceChange,
                claims.ToArray(),
                () => request.CreateResponse(HttpStatusCode.Created),
                (why) => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Authentication failed:{why}"),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Credential already exists"),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Relationship already exists"),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Login is already in use"),
                () => request.CreateResponse(HttpStatusCode.ServiceUnavailable),
                (why) => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason(why));
            return creationResults;
            //},
            //() => request.CreateResponse(HttpStatusCode.Unauthorized).ToTask(),
            //(why) => request.CreateResponse(HttpStatusCode.InternalServerError).AddReason(why).ToTask());
        }

        public static async Task<HttpResponseMessage> PutAsync(this Resources.PasswordCredential credential,
            HttpRequestMessage request)
        {
            var context = request.GetSessionServerContext();
            var creationResults = await context.Authorizations.UpdateCredentialsAsync(credential.Id.UUID,
                credential.UserId, credential.IsEmail, credential.Token, credential.ForceChange,
                () => request.CreateResponse(HttpStatusCode.NoContent),
                () => request.CreateResponse(HttpStatusCode.Conflict).AddReason("Authorization does not exist"),
                (why) => request.CreateResponse(HttpStatusCode.Conflict).AddReason($"Update failed:{why}"));
            return creationResults;
        }

        #region Actionables

        public static async Task<HttpResponseMessage> QueryAsync(this Resources.Queries.PasswordCredentialQuery credential,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return await credential.ParseAsync(request,
                q => QueryByIdAsync(q.Id.ParamSingle(), request, urlHelper),
                q => QueryByActorId(q.Actor.ParamSingle(), request));
        }

        private static async Task<HttpResponseMessage> QueryByIdAsync(Guid passwordCredentialId, HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.CredentialMappings.GetPasswordCredentialAsync(passwordCredentialId,
                (passwordCredential) =>
                {
                    var response = request.CreateResponse(HttpStatusCode.OK, new Resources.PasswordCredential
                    {
                        Id = passwordCredential.id,
                        Actor = passwordCredential.actorId,
                        UserId = passwordCredential.userId,
                        IsEmail = passwordCredential.isEmail,
                        ForceChange = passwordCredential.forceChangePassword,
                        Token = "************",
                    });
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound),
                (why) => request.CreateResponse(HttpStatusCode.NotFound));
        }

        private async static Task<HttpResponseMessage> QueryByActorId(Guid actorId, HttpRequestMessage request)
        {
            var context = request.GetSessionServerContext();

            return await context.CredentialMappings.GetPasswordCredentialByActorAsync(
                actorId,
                (id) => request.CreateResponse(HttpStatusCode.OK, id),
                () => request.CreateResponse(HttpStatusCode.NotFound),
                (why) => request.CreateResponse(HttpStatusCode.ServiceUnavailable).AddReason(why));
        }

        public static Task<HttpResponseMessage> DeleteAsync(this Resources.Queries.PasswordCredentialQuery credential,
            HttpRequestMessage request)
        {
            return request
                .CreateResponse(HttpStatusCode.Unauthorized)
                .AddReason("Deleting Credential Resource is not yet supported")
                .ToTask();
        }

        public static Task<HttpResponseMessage> CredentialOptionsAsync(this HttpRequestMessage request)
        {
            var credentialProviders = new Resources.PasswordCredential[]
            {
                new Resources.PasswordCredential
                {
                    UserId = "0123456789",
                    Token = "ABC.123.MXC",
                },
                new Resources.PasswordCredential
                {
                    //Method = CredentialValidationMethodTypes.OpenIdConnect,
                    //Provider = new Uri("urn:auth.gibbits.nc2media.com/AuthOpenIdConnect/"),
                    UserId = Guid.NewGuid().ToString("N"),
                    Token = "EDF.123.A3EF",
                },
                new Resources.PasswordCredential
                {
                    //Method = CredentialValidationMethodTypes.Implicit,
                    //Provider = new Uri("http://www.example.com/ImplicitAuth"),
                    UserId = Guid.NewGuid().ToString("N"),
                    Token = Guid.NewGuid().ToString("N"),
                }
            };
            var response = new BlackBarLabs.Api.Resources.Options()
            {
                Get = credentialProviders,
            };

            var responseMessage = request.CreateResponse(System.Net.HttpStatusCode.OK, response);
            return responseMessage.ToTask();
        }

        #endregion
    }
}

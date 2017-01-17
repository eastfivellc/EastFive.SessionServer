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

namespace EastFive.Security.SessionServer.Api
{
    public static class CredentialActions
    {
        #region Actionables

        public static async Task<HttpResponseMessage> QueryAsync(this Resources.Queries.CredentialQuery credential,
            HttpRequestMessage request)
        {
            return await credential.ParseAsync(request,
                q => QueryByAuthId(q.AuthorizationId.ParamSingle(), request));
        }

        private async static Task<HttpResponseMessage> QueryByAuthId(Guid authorizationId, HttpRequestMessage request)
        {
            var context = request.GetSessionServerContext();

            return await context.Authorizations.GetCredentialsAsync(
                authorizationId,
                (id) => request.CreateResponse(HttpStatusCode.OK, id),
                () => request.CreateResponse(HttpStatusCode.NotFound));
        }

        public static async Task<HttpResponseMessage> CreateAsync(this Resources.Credential credential,
            HttpRequestMessage request)
        {
            //return await request.GetClaims(
            //    async (claims) =>
            //    {
            var claims = new System.Security.Claims.Claim[] { };
                    var context = request.GetSessionServerContext();
                    var creationResults = await context.Authorizations.CreateCredentialsAsync(credential.AuthorizationId.UUID,
                        credential.Method,
                        credential.UserId, credential.IsEmail, credential.Token, credential.ForceChange,
                        claims.ToArray(),
                        () => request.CreateResponse(HttpStatusCode.Created, credential),
                        (why) => request.CreateResponse(HttpStatusCode.Conflict).AddReason($"Authentication failed:{why}"),
                        () =>
                        {
                            return request.CreateResponse(HttpStatusCode.Conflict).AddReason("Credential is already associated with an actor.");
                        });
                    return creationResults;
                //},
                //() => request.CreateResponse(HttpStatusCode.Unauthorized).ToTask(),
                //(why) => request.CreateResponse(HttpStatusCode.InternalServerError).AddReason(why).ToTask());
        }

        public static async Task<HttpResponseMessage> PutAsync(this Resources.Credential credential,
            HttpRequestMessage request)
        {
            var context = request.GetSessionServerContext();
            var creationResults = await context.Authorizations.UpdateCredentialsAsync(credential.AuthorizationId.UUID,
                credential.UserId, credential.IsEmail, credential.Token, credential.ForceChange,
                () => request.CreateResponse(HttpStatusCode.NoContent),
                () => request.CreateResponse(HttpStatusCode.Conflict).AddReason("Authorization does not exist"),
                (why) => request.CreateResponse(HttpStatusCode.Conflict).AddReason($"Update failed:{why}"));
            return creationResults;
        }

        public static Task<HttpResponseMessage> DeleteAsync(this Resources.Queries.CredentialQuery credential,
            HttpRequestMessage request)
        {
            return request
                .CreateResponse(HttpStatusCode.Unauthorized)
                .AddReason("Deleting Credential Resource is not yet supported")
                .ToTask();
        }

        public static Task<HttpResponseMessage> CredentialOptionsAsync(this HttpRequestMessage request)
        {
            var credentialProviders = new Resources.Credential[]
            {
                new Resources.Credential
                {   
                    UserId = "0123456789",
                    Token = "ABC.123.MXC",
                },
                new Resources.Credential
                {
                    //Method = CredentialValidationMethodTypes.OpenIdConnect,
                    //Provider = new Uri("urn:auth.gibbits.nc2media.com/AuthOpenIdConnect/"),
                    UserId = Guid.NewGuid().ToString("N"),
                    Token = "EDF.123.A3EF",
                },
                new Resources.Credential
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

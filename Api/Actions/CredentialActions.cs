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
using EastFive.Api.Azure.Credentials.Api.Resources;
using EastFive.Security.SessionServer;

namespace EastFive.Api.Azure.Credentials
{
    public static class CredentialActions
    {
        public static async Task<HttpResponseMessage> CreateAsync(this Credential credential,
            HttpRequestMessage request, UrlHelper url)
        {
            return await request.GetActorIdClaimsAsync(
                async (performingActorId, claims) =>
                {
                    var authenticationId = credential.Authentication.ToGuid();
                    if (!authenticationId.HasValue)
                        return request.CreateResponseEmptyId(credential, c => c.Authentication);

                    var credentialId = credential.Id.ToGuid();
                    if (!credentialId.HasValue)
                        return request.CreateResponseEmptyId(credential, c => c.Id);

                    var context = request.GetSessionServerContext();
                    var creationResults = await context.Credentials.CreateAsync(
                            credentialId.Value, authenticationId.Value,
                            Enum.GetName(typeof(CredentialValidationMethodTypes), credential.Method), credential.Subject,
                            performingActorId, claims,
                        () => request.CreateResponse(HttpStatusCode.Created),
                        (credentialIdExisting) => request
                            .CreateAlreadyExistsResponse<Controllers.CredentialController>(
                                credentialIdExisting, url),
                        () => request.CreateResponse(HttpStatusCode.Conflict)
                            .AddReason($"Credentail is already mapped"),
                        () => request.CreateResponse(HttpStatusCode.Unauthorized),
                        (why) => request.CreateResponse(HttpStatusCode.Conflict)
                            .AddReason(why));
                    return creationResults;
                });
        }
    }
}

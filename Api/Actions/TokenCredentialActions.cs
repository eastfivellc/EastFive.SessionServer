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
    public static class TokenCredentialActions
    {
        #region Queries
        
        public static async Task<HttpResponseMessage> QueryAsync(this Resources.Queries.TokenCredentialQuery query,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return await query.ParseAsync(request,
                q => QueryByIdAsync(q.Id.ParamSingle(), request, urlHelper),
                q => QueryByTokenAsync(q.Token.ParamSingle(), request, urlHelper),
                q => QueryByActorAsync(q.Actor.ParamSingle(), request, urlHelper));
        }

        private static async Task<HttpResponseMessage> QueryByIdAsync(Guid inviteId, 
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.CredentialMappings.GetTokenCredentialAsync(inviteId,
                (invite) =>
                {
                    var response = request.CreateResponse(HttpStatusCode.OK,
                        new Resources.TokenCredential
                        {
                            Id = invite.id,
                            ActorId = invite.actorId,
                            Email = invite.email,
                            LastSent = invite.lastSent,
                        });
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound));
        }

        private static async Task<HttpResponseMessage> QueryByTokenAsync(Guid token, 
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.CredentialMappings.GetTokenCredentialByTokenAsync(token,
                (sessionId, actorId, jwtToken, refreshToken) =>
                {
                    var redirectUrl = new Uri("http://orderowl.com/Login")
                        .SetQueryParam("sessionId", sessionId.ToString("N"))
                        .SetQueryParam("actorId", actorId.ToString("N"))
                        .SetQueryParam("token", jwtToken)
                        .SetQueryParam("refreshToken", refreshToken);
                    var response = request.CreateResponse(HttpStatusCode.Redirect);
                    response.Headers.Location = redirectUrl;
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound));
        }

        private static async Task<HttpResponseMessage[]> QueryByActorAsync(Guid actorId, HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.CredentialMappings.GetTokenCredentialByActorAsync(actorId,
                (invites) =>
                {
                    var responses = invites
                        .Select(invite =>
                            request.CreateResponse(HttpStatusCode.OK, Convert(invite, urlHelper)))
                        .ToArray();
                    return responses;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound).ToEnumerable().ToArray());
        }

        private static Resources.TokenCredential Convert(Invite invite, UrlHelper urlHelper)
        {
            return new Resources.TokenCredential
            {
                Id = invite.id,
                ActorId = invite.actorId,
                Email = invite.email,
                LastSent = invite.lastSent,
            };
        }

        #endregion

        public static async Task<HttpResponseMessage> CreateAsync(this Resources.TokenCredential credential,
            HttpRequestMessage request, UrlHelper url)
        {
            var actorId = credential.ActorId;
            //return await request.GetClaims(
            //    async (claims) =>
            //    {
            var claims = new System.Security.Claims.Claim[] { };
            var context = request.GetSessionServerContext();
            var creationResults = await context.CredentialMappings.CreateTokenCredentialAsync(
                credential.Id.UUID, actorId, credential.Email,
                claims.ToArray(),
                (inviteId, token) => url.GetLocation<Controllers.TokenCredentialController>().SetQueryParam("token", token.ToString("N")),
                () => request.CreateResponse(HttpStatusCode.Created),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Invite already exists"),
                () => request.CreateResponse(HttpStatusCode.ServiceUnavailable),
                (why) => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason(why));
            return creationResults;
            //},
            //() => request.CreateResponse(HttpStatusCode.Unauthorized).ToTask(),
            //(why) => request.CreateResponse(HttpStatusCode.InternalServerError).AddReason(why).ToTask());
        }
        
        public static async Task<HttpResponseMessage> UpdateAsync(this Resources.TokenCredential credential,
            HttpRequestMessage request, UrlHelper url)
        {
            var actorId = credential.ActorId;
            //return await request.GetClaims(
            //    async (claims) =>
            //    {
            var claims = new System.Security.Claims.Claim[] { };
            var context = request.GetSessionServerContext();
            var creationResults = await context.CredentialMappings.UpdateTokenCredentialAsync(
                credential.Id.UUID, credential.Email, credential.LastSent,
                claims.ToArray(),
                (inviteId, token) => url.GetLocation<Controllers.TokenCredentialController>()
                    .SetQueryParam("token", token.ToString("N")),
                () => request.CreateResponse(HttpStatusCode.Accepted),
                () => request.CreateResponse(HttpStatusCode.NotModified),
                () => request.CreateResponse(HttpStatusCode.NotFound)
                    .AddReason($"TokenCredential not found"),
                () => request.CreateResponse(HttpStatusCode.ServiceUnavailable),
                (why) => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason(why));
            return creationResults;
            //},
            //() => request.CreateResponse(HttpStatusCode.Unauthorized).ToTask(),
            //(why) => request.CreateResponse(HttpStatusCode.InternalServerError).AddReason(why).ToTask());
        }
    }
}

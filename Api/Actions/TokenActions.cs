using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http.Routing;
using BlackBarLabs.Api;
using System.Linq;
using BlackBarLabs;
using BlackBarLabs.Extensions;
using EastFive.Security.SessionServer.Api.Controllers;

namespace EastFive.Security.SessionServer.Api
{
    public static class TokenActions
    {
        public static async Task<HttpResponseMessage> GetAsync(this Resources.TokenQuery query, HttpRequestMessage request, UrlHelper url)
        {
            return await query.ParseAsync(request,
                (q) => QueryByActorId(q.Actor.ParamSingle(), request, url));
        }

        private static async Task<HttpResponseMessage> QueryByActorId(Guid actorId, HttpRequestMessage request, UrlHelper url)
        {
            return await request.GetActorIdClaimsAsync(
                async (actingAsActorId, claims) =>
                {
                    var context = request.GetSessionServerContext();
                    return await context.Sessions.CreateToken(actorId, Guid.NewGuid(), actingAsActorId,
                        (token, refreshToken) =>
                        {
                            return request.CreateResponse(HttpStatusCode.OK, token);
                        },
                        () => request.CreateResponse(HttpStatusCode.Unauthorized),
                        () => request.CreateResponse(HttpStatusCode.NotFound).AddReason("Actor not found"),
                        (why) => request.CreateResponse(HttpStatusCode.InternalServerError).AddReason(why));
                });
        }
    }
}
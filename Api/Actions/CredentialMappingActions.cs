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
    public static class CredentialMappingActions
    {
        #region Queries
        
        //public static async Task<HttpResponseMessage> QueryAsync(this Resources.Queries.CredentialMappingQuery query,
        //    HttpRequestMessage request, UrlHelper urlHelper)
        //{
        //    return await query.ParseAsync(request,
        //        q => QueryByIdAsync(q.Id.ParamSingle(), request, urlHelper),
        //        q => QueryByActorAsync(q.Actor.ParamSingle(), request, urlHelper));
        //}

        //private static async Task<HttpResponseMessage> QueryByIdAsync(Guid credentialMappingId,
        //    HttpRequestMessage request, UrlHelper urlHelper)
        //{
        //    var context = request.GetSessionServerContext();
        //    return await context.CredentialMappings.GetAsync(credentialMappingId,
        //        (actorId, loginId) =>
        //        {
        //            var response = request.CreateResponse(HttpStatusCode.OK, new Resources.CredentialMapping
        //            {
        //                Id = credentialMappingId,
        //                ActorId = actorId,
        //                LoginId = loginId,
        //            });
        //            return response;
        //        },
        //        () => request.CreateResponse(HttpStatusCode.NotFound));
        //}

        //private static async Task<HttpResponseMessage[]> QueryByActorAsync(Guid actorId,
        //    HttpRequestMessage request, UrlHelper urlHelper)
        //{
        //    var context = request.GetSessionServerContext();
        //    return await context.CredentialMappings.GetByActorAsync(actorId,
        //        (credenialMappings) =>
        //        {
        //            var responses = credenialMappings
        //                .Select(credenialMapping =>
        //                    request.CreateResponse(HttpStatusCode.OK, Convert(credenialMapping, urlHelper)))
        //                .ToArray();
        //            return responses;
        //        },
        //        () => request.CreateResponse(HttpStatusCode.NotFound).ToEnumerable().ToArray());
        //}

        //private static Resources.CredentialMapping Convert(CredentialMapping credenialMapping, UrlHelper urlHelper)
        //{
        //    return new Resources.CredentialMapping
        //    {
        //        Id = credenialMapping.id,
        //        ActorId = credenialMapping.actorId,
        //        LoginId = credenialMapping.loginId,
        //    };
        //}

        #endregion
        
        //internal static async Task<HttpResponseMessage> CreateAsync(this Resources.CredentialMapping resource,
        //    HttpRequestMessage request, UrlHelper urlHelper)
        //{
        //    var credentialMappingId = resource.Id.ToGuid();
        //    if (!credentialMappingId.HasValue)
        //        return request.CreateResponse(HttpStatusCode.BadRequest)
        //            .AddReason("Cannot create resource without Id");
        //    var actorId = resource.ActorId.ToGuid();
        //    if (!actorId.HasValue)
        //        return request.CreateResponse(HttpStatusCode.BadRequest)
        //            .AddReason("Actor Id is required");

        //    var claims = new System.Security.Claims.Claim[] { };
        //    var context = request.GetSessionServerContext();
        //    var creationResults = await context.CredentialMappings.CreateAsync(credentialMappingId.Value,
        //        actorId.Value, resource.LoginId.ToGuid(),
        //        claims.ToArray(),
        //        () => request.CreateResponse(HttpStatusCode.Created),
        //        () => request.CreateResponse(HttpStatusCode.Conflict)
        //            .AddReason($"Mapping already exists"),
        //        () => request.CreateResponse(HttpStatusCode.Conflict)
        //            .AddReason($"Login already exists"));
        //    return creationResults;
        //}
    }
}

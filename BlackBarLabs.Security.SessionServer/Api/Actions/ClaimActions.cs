using BlackBarLabs.Api;
using BlackBarLabs.Collections.Async;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace EastFive.Security.SessionServer.Api
{
    public static class ClaimActions
    {
        public static async Task<HttpResponseMessage> GetAsync(this Api.Resources.Claim claim, HttpRequestMessage request)
        {
            var context = request.GetSessionServerContext();
            return await context.Claims.FindAsync(claim.AuthorizationId, claim.Type,
                (claimsAsync) =>
                {
                    var claims = claimsAsync.ToEnumerable((Guid claimId, Guid authorizationId, Uri type, string value) => new Resources.Claim()
                    {
                        Id = claimId,
                        AuthorizationId = authorizationId,
                        Type = type,
                        Value = value,
                    });
                    return request.CreateResponse(HttpStatusCode.OK, claims.ToArray());
                },
                () => request.CreateResponse(HttpStatusCode.NotFound, "Authorization does not exists"),
                (message) => request.CreateResponse(HttpStatusCode.Conflict, message));
        }

        public static async Task<HttpResponseMessage> CreateAsync(this Resources.Claim resource,
            HttpRequestMessage request)
        {
            var context = request.GetSessionServerContext();
            return await context.Claims.CreateAsync(resource.Id,
                resource.AuthorizationId, resource.Issuer, resource.Type, resource.Value, resource.Signature,
                () => request.CreateResponse(HttpStatusCode.Created),
                () => request.CreateResponse(HttpStatusCode.Conflict, "Authorization does not exists"),
                () => request.CreateResponse(HttpStatusCode.Conflict, "Claim already exists for that authorization"),
                (message) => request.CreateResponse(HttpStatusCode.Conflict, new Exception(message)));
        }
        
        public static async Task<HttpResponseMessage> UpdateAsync(this Resources.Claim resource,
            HttpRequestMessage request)
        {
            var context = request.GetSessionServerContext();
            return await context.Claims.UpdateAsync(resource.Id,
                resource.AuthorizationId, resource.Issuer, resource.Type, resource.Value, resource.Signature,
                () => request.CreateResponse(HttpStatusCode.Created),
                () => request.CreateResponse(HttpStatusCode.Conflict).AddReason("Authorization does not exists"),
                () => request.CreateResponse(HttpStatusCode.Conflict).AddReason("Claim not found for that authorization"),
                (message) => request.CreateResponse(HttpStatusCode.Conflict).AddReason(message));
        }

    }
}
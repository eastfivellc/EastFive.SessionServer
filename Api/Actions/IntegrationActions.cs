using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

using BlackBarLabs.Api;
using System.Web.Http.Routing;
using BlackBarLabs.Extensions;

namespace EastFive.Security.SessionServer.Api
{
    public static class IntegrationActions
    {
        public static async Task<HttpResponseMessage> CreateAsync(this Resources.Integration authenticationRequest,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            var credentialId = authenticationRequest.Id.ToGuid();
            if (!credentialId.HasValue)
                return request.CreateResponse(HttpStatusCode.BadRequest).AddReason("Id must have value");
            
            if (authenticationRequest.AuthorizationId.IsDefault())
                return request.CreateResponseEmptyId(authenticationRequest, ar => ar.AuthorizationId)
                    .AddReason("Authorization Id must have value for integration");

            return await request.GetActorIdClaimsAsync(
                (actorId, claims) =>
                    context.Integrations.CreateLinkAsync(credentialId.Value,
                        urlHelper.GetLocation<Controllers.OpenIdResponseController>(),
                        authenticationRequest.Method, authenticationRequest.LocationAuthenticationReturn,
                        authenticationRequest.AuthorizationId, actorId, claims,
                        (authenticationRequestPopulated) =>
                        {
                            var resource = Convert(authenticationRequestPopulated, urlHelper);
                            return request.CreateResponse(HttpStatusCode.Created, resource);
                        },
                        () => request.CreateAlreadyExistsResponse<Controllers.SessionController>(
                            credentialId.Value, urlHelper),
                        (why) => request.CreateResponse(HttpStatusCode.Unauthorized).AddReason(why),
                        () => request.CreateResponse(HttpStatusCode.BadRequest)
                            .AddReason($"Method [{authenticationRequest.Method}] is not enabled for this system"),
                        (why) => request.CreateResponse(HttpStatusCode.ServiceUnavailable)
                            .AddReason(why),
                        (why) => request.CreateResponse(HttpStatusCode.InternalServerError)
                            .AddReason(why)));
        }

        
        #region Actionables

        public static Task<HttpResponseMessage> QueryAsync(this Resources.Queries.IntegrationQuery query,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return request.GetActorIdClaimsAsync(
                (actingAs, claims) => query.ParseAsync(request,
                    q => QueryByIdAsync(q.Id.ParamSingle(), actingAs, claims, request, urlHelper),
                    q => QueryByActorAsync(q.ActorId.ParamSingle(), actingAs, claims, request, urlHelper)));
        }

        private static async Task<HttpResponseMessage> QueryByIdAsync(Guid authenticationRequestId,
                Guid actingAs, System.Security.Claims.Claim[] claims,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.Integrations.GetAsync(authenticationRequestId,
                    urlHelper.GetLocation<Controllers.ResponseController>(),
                (authenticationRequest) =>
                {
                    var response = request.CreateResponse(HttpStatusCode.OK,
                        Convert(authenticationRequest, urlHelper));
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound),
                (why) => request.CreateResponseUnexpectedFailure(why));
        }

        private static async Task<HttpResponseMessage[]> QueryByActorAsync(Guid actorId,
                Guid actingAs, System.Security.Claims.Claim[] claims,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.Integrations.GetByActorAsync(actorId,
                    urlHelper.GetLocation<Controllers.ResponseController>(),
                    actingAs, claims,
                (authenticationRequests) =>
                {
                    var response = authenticationRequests.Select(authenticationRequest => request.CreateResponse(HttpStatusCode.OK,
                        Convert(authenticationRequest, urlHelper))).ToArray();
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound).AsArray(),
                () => request.CreateResponse(HttpStatusCode.Unauthorized).AsArray(),
                (why) => request.CreateResponseUnexpectedFailure(why).AsArray());
        }

        private static Resources.Integration Convert(Session authenticationRequest, UrlHelper urlHelper)
        {
            return new Resources.Integration
            {
                Id = urlHelper.GetWebId<Controllers.IntegrationController>(authenticationRequest.id),
                Method = authenticationRequest.method,
                AuthorizationId = authenticationRequest.authorizationId.HasValue?
                    authenticationRequest.authorizationId.Value
                    :
                    default(Guid),
                ExtraParams = authenticationRequest.extraParams,
                LocationAuthentication = authenticationRequest.loginUrl,
                LocationAuthenticationReturn = authenticationRequest.redirectUrl,
            };
        }

        public static async Task<HttpResponseMessage> UpdateAsync(this Api.Resources.Integration resource,
            HttpRequestMessage request)
        {
            var context = request.GetSessionServerContext();
            // Can't update a session that does not exist
            var session = await context.Sessions.AuthenticateAsync(resource.Id.ToGuid().Value,
                resource.Method, resource.ResponseToken,
                (sessionId, authId, token, refreshToken, action, extraParams, redirectUrl) =>
                {
                    resource.AuthorizationId = authId;
                    return request.CreateResponse(HttpStatusCode.Accepted, resource);
                },
                (why) => request.CreateResponse(HttpStatusCode.NotFound).AddReason(why),
                () => request.CreateResponse(HttpStatusCode.Conflict).AddReason("User in token is not connected to this system"),
                (why) => request.CreateResponse(HttpStatusCode.BadGateway).AddReason(why),
                (why) => request.CreateResponseConfiguration(string.Empty, why),
                (why) => request.CreateResponseUnexpectedFailure(why));
            return session;
        }

        public static async Task<HttpResponseMessage> DeleteAsync(this Resources.Queries.IntegrationQuery credential,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return await request.GetActorIdClaimsAsync(
                (actingAs, claims) =>
                    credential.ParseAsync(request,
                        q => DeleteByIdAsync(q.Id.ParamSingle(), actingAs, claims, request, urlHelper),
                        q => DeleteByActorAsync(q.ActorId.ParamSingle(), actingAs, claims, request, urlHelper)));
        }

        private static async Task<HttpResponseMessage> DeleteByIdAsync(Guid integrationId,
                Guid actingAs, System.Security.Claims.Claim [] claims,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.Integrations.DeleteByIdAsync(integrationId,
                    actingAs, claims,
                () =>
                {
                    var response = request.CreateResponse(HttpStatusCode.NoContent);
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound),
                () => request.CreateResponse(HttpStatusCode.Unauthorized));
        }

        private static async Task<HttpResponseMessage> DeleteByActorAsync(Guid actorId,
                Guid actingAs, System.Security.Claims.Claim[] claims,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.Integrations.DeleteByActorAsync(actorId,
                    actingAs, claims,
                () =>
                {
                    var response = request.CreateResponse(HttpStatusCode.NoContent);
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.Unauthorized));
        }

        #endregion
    }
}
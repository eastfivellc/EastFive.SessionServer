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

namespace EastFive.Security.SessionServer.Api
{
    public static class AuthenticationRequestActions
    {
        public static async Task<HttpResponseMessage> CreateAsync(this Resources.AuthenticationRequest authenticationRequest,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            var credentialId = authenticationRequest.Id.ToGuid();
            if (!credentialId.HasValue)
                return request.CreateResponse(HttpStatusCode.BadRequest).AddReason("Id must have value");

            if (authenticationRequest.SessionId.HasValue)
                return request.CreateResponse(HttpStatusCode.BadRequest).AddReason("Session Id value is provided by the server");

            if(AuthenticationActions.signin == authenticationRequest.Action)
            {
                return await context.AuthenticationRequests.CreateLoginAsync(credentialId.Value,
                    urlHelper.GetLocation<Controllers.OpenIdResponseController>(),
                    authenticationRequest.Method, authenticationRequest.Redirect,
                    (authenticationRequestPopulated) =>
                    {
                        var resource = Convert(authenticationRequestPopulated, urlHelper);
                        return request.CreateResponse(HttpStatusCode.Created, resource);
                    },
                    () => request.CreateResponseNotFound(credentialId.Value),
                    () => request.CreateResponse(HttpStatusCode.BadRequest)
                        .AddReason($"Method [{authenticationRequest.Method}] is not enabled for this system"),
                    (why) => request.CreateResponse(HttpStatusCode.ServiceUnavailable)
                        .AddReason(why),
                    (why) => request.CreateResponse(HttpStatusCode.InternalServerError)
                        .AddReason(why));
            }

            if (!authenticationRequest.AuthorizationId.HasValue)
                return request.CreateResponse(HttpStatusCode.BadRequest)
                    .AddReason("Authorization Id must have value for linked authentication");

            return await request.GetActorIdClaimsAsync(
                (actorId, claims) =>
                    context.AuthenticationRequests.CreateLinkAsync(credentialId.Value,
                        urlHelper.GetLocation<Controllers.OpenIdResponseController>(),
                        authenticationRequest.Method, authenticationRequest.Redirect,
                        authenticationRequest.AuthorizationId.Value, actorId, claims,
                        (authenticationRequestPopulated) =>
                        {
                            var resource = Convert(authenticationRequestPopulated, urlHelper);
                            return request.CreateResponse(HttpStatusCode.Created, resource);
                        },
                        () => request.CreateAlreadyExistsResponse<Controllers.AuthenticationRequestController>(
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

        public static Task<HttpResponseMessage> QueryAsync(this Resources.Queries.AuthenticationRequestQuery query,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return request.GetSessionIdClaimsAsync(
                (sessionId, claims) => query.ParseAsync(request,
                    q => QueryByIdAsync(q.Id.ParamSingle(), request, urlHelper, sessionId, claims)));
        }

        private static async Task<HttpResponseMessage> QueryByIdAsync(Guid authenticationRequestId,
            HttpRequestMessage request, UrlHelper urlHelper,
            Guid sessionId, System.Security.Claims.Claim [] claims)
        {
            var context = request.GetSessionServerContext();
            return await context.AuthenticationRequests.GetAsync(authenticationRequestId,
                    urlHelper.GetLocation<Controllers.OpenIdResponseController>(),
                    sessionId, claims,
                (authenticationRequest) =>
                {
                    var response = request.CreateResponse(HttpStatusCode.OK,
                        Convert(authenticationRequest, urlHelper));
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound),
                () => request.CreateResponse(HttpStatusCode.Unauthorized),
                (why) => request.CreateResponse(HttpStatusCode.InternalServerError).AddReason(why));
        }

        private static Resources.AuthenticationRequest Convert(AuthenticationRequest authenticationRequest, UrlHelper urlHelper)
        {
            return new Resources.AuthenticationRequest
            {
                Id = urlHelper.GetWebId<Controllers.AuthenticationRequestController>(authenticationRequest.id),
                Method = authenticationRequest.method,
                SessionId = authenticationRequest.sessionId,
                AuthorizationId = authenticationRequest.authorizationId,
                JwtToken = authenticationRequest.token,
                RefreshToken = authenticationRequest.refreshToken,
                ExtraParams = authenticationRequest.extraParams,
                Redirect = authenticationRequest.redirectUrl,
                Login = authenticationRequest.loginUrl,
            };
        }

        public static async Task<HttpResponseMessage> DeleteAsync(this Resources.Queries.AuthenticationRequestQuery credential,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return await credential.ParseAsync(request,
                q => DeleteByIdAsync(q.Id.ParamSingle(), request, urlHelper));
        }

        private static async Task<HttpResponseMessage> DeleteByIdAsync(Guid passwordCredentialId, HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.PasswordCredentials.DeletePasswordCredentialAsync(passwordCredentialId,
                () =>
                {
                    var response = request.CreateResponse(HttpStatusCode.NoContent);
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound),
                (why) => request.CreateResponse(HttpStatusCode.NotFound));
        }

        #endregion
    }
}

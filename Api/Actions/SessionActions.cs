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

namespace EastFive.Security.SessionServer.Api
{
    public static class SessionActions
    {
        public static async Task<HttpResponseMessage> CreateAsync(this Resources.Session authenticationRequest,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            var credentialId = authenticationRequest.Id.ToGuid();
            if (!credentialId.HasValue)
                return request.CreateResponse(HttpStatusCode.BadRequest).AddReason("Id must have value");

            return await context.Sessions.CreateLoginAsync(credentialId.Value,
                    authenticationRequest.Method,
                    authenticationRequest.LocationAuthenticationReturn,
                    authenticationRequest.LocationLogoutReturn,
                    (controllerType) => urlHelper.GetLocation(controllerType),
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
        
        #region Actionables

        public static Task<HttpResponseMessage> QueryAsync(this Resources.Queries.SessionQuery query,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return query.ParseAsync(request,
                    q => QueryByIdAsync(q.Id.ParamSingle(), request, urlHelper));
        }

        private static async Task<HttpResponseMessage> QueryByIdAsync(Guid authenticationRequestId,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.Sessions.GetAsync(authenticationRequestId,
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

        private static Resources.Session Convert(Session authenticationRequest, UrlHelper urlHelper)
        {
            return new Resources.Session
            {
                Id = urlHelper.GetWebId<Controllers.SessionController>(authenticationRequest.id),
                Method = authenticationRequest.method,
                AuthorizationId = authenticationRequest.authorizationId,
                HeaderName = EastFive.Api.Configuration.SecurityDefinitions.AuthorizationHeader,
                Token = authenticationRequest.token,
                RefreshToken = authenticationRequest.refreshToken,
                ExtraParams = authenticationRequest.extraParams,
                LocationAuthentication = authenticationRequest.loginUrl,
                LocationAuthenticationReturn = authenticationRequest.redirectUrl,
                LocationLogout = authenticationRequest.logoutUrl,
                LocationLogoutReturn = authenticationRequest.redirectLogoutUrl,
            };
        }

        public static async Task<HttpResponseMessage> UpdateAsync(this Api.Resources.Session resource,
            HttpRequestMessage request)
        {
            var context = request.GetSessionServerContext();
            // Can't update a session that does not exist
            var session = await context.Sessions.AuthenticateAsync(resource.Id.ToGuid().Value,
                resource.Method, resource.ResponseToken,
                (sessionId, authId, token, refreshToken, actions, extraParams, redirect) =>
                {
                    resource.AuthorizationId = authId;
                    resource.HeaderName = EastFive.Api.Configuration.SecurityDefinitions.AuthorizationHeader;
                    resource.Token = token;
                    resource.RefreshToken = refreshToken;
                    resource.ExtraParams = extraParams;
                    return request.CreateResponse(HttpStatusCode.Accepted, resource);
                },
                (location) => request.CreateRedirectResponse(location),
                (why) => request.CreateResponse(HttpStatusCode.NotFound).AddReason(why),
                () => request.CreateResponse(HttpStatusCode.Conflict).AddReason("User in token is not connected to this system"),
                (why) => request.CreateResponse(HttpStatusCode.BadGateway).AddReason(why),
                (why) => request.CreateResponseConfiguration(string.Empty, why),
                (why) => request.CreateResponseUnexpectedFailure(why));
            return session;
        }

        public static async Task<HttpResponseMessage> DeleteAsync(this Resources.Queries.SessionQuery query,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return await query.ParseAsync(request,
                q => DeleteByIdAsync(q.Id.ParamSingle(), request, urlHelper));
        }

        private static async Task<HttpResponseMessage> DeleteByIdAsync(Guid sessionId, HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.Sessions.DeleteAsync(sessionId, urlHelper.GetLocation<Controllers.ResponseController>(),
                (sessionDeleted) =>
                {
                    var response = request.CreateResponse(HttpStatusCode.Accepted,
                        Convert(sessionDeleted, urlHelper));
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound),
                (why) => request.CreateResponse(HttpStatusCode.NotFound).AddReason(why));
        }

        #endregion
    }
}
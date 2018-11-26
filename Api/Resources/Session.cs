﻿using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using EastFive.Api.Controllers;
using EastFive.Security.SessionServer;
using EastFive.Security.SessionServer.Api.Resources;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http.Routing;

namespace EastFive.Api.Azure.Credentials.Resources
{
    [DataContract]
    [FunctionViewController(Route = "Session",
        Resource = typeof(Session),
        ContentType = "application/x-session+json")]
    public class Session : AuthorizationRequest
    {
        [DataMember]
        [JsonProperty(PropertyName = "location_logout")]
        public Uri LocationLogout { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "location_logout_return")]
        public Uri LocationLogoutReturn { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "authorization_id")]
        public Guid? AuthorizationId { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "header_name")]
        public string HeaderName { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "token")]
        public string Token { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "refresh_token")]
        public string RefreshToken { get; set; }

        [HttpPost(MatchAllBodyParameters = false)]
        public static async Task<HttpResponseMessage> CreateAsync(Resources.Session authenticationRequest,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            var credentialId = authenticationRequest.Id.ToGuid();
            if (!credentialId.HasValue)
                return request.CreateResponse(HttpStatusCode.BadRequest).AddReason("Id must have value");

            var method = authenticationRequest.Method;
            return await context.Sessions.CreateLoginAsync(credentialId.Value,
                    method,
                    authenticationRequest.LocationAuthenticationReturn,
                    authenticationRequest.LocationLogoutReturn,
                    (controllerType) => urlHelper.GetLocation(controllerType),
                (authenticationRequestPopulated) =>
                {
                    var resource = Convert(authenticationRequestPopulated, urlHelper);
                    return request.CreateResponse(HttpStatusCode.Created, resource);
                },
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason("The session has already been created"),
                () => request.CreateResponse(HttpStatusCode.BadRequest)
                    .AddReason($"Method [{authenticationRequest.Method}] is not enabled for this system"),
                (why) => request.CreateResponse(HttpStatusCode.ServiceUnavailable)
                    .AddReason(why),
                (why) => request.CreateResponse(HttpStatusCode.InternalServerError)
                    .AddReason(why));
        }

        #region Actionables
        
        [HttpGet]
        public static async Task<HttpResponseMessage> QueryByIdAsync(
                [QueryParameter(CheckFileName = true, Name = IdPropertyName)]Guid authenticationRequestId,
                Context context, UrlHelper urlHelper, AzureApplication application,
            ContentResponse onFound,
            NotFoundResponse onNotFound,
            GeneralFailureResponse onFailure)
        {
            return await context.Sessions.GetAsync(authenticationRequestId,
                    (type) => urlHelper.GetLocation(type), 
                    application,
                (authenticationRequest) =>
                {
                    var resource = Convert(authenticationRequest, urlHelper);
                    return onFound(resource);
                },
                (why) => onNotFound().AddReason(why),
                (why) => onFailure(why));
        }

        private static Resources.Session Convert(Security.SessionServer.Session authenticationRequest, UrlHelper urlHelper)
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

        [HttpPut]
        public static Task<HttpResponseMessage> UpdateAsync(Resources.Session resource,
            HttpRequestMessage request)
        {
            var context = request.GetSessionServerContext();
            // Can't update a session that does not exist
            var method = resource.Method;
            return request.GetApplication(
                async application =>
                {
                    var session = await context.Sessions.UpdateWithAuthenticationAsync(resource.Id.ToGuid().Value,
                        application as AzureApplication, method, resource.ResponseToken,
                        (sessionId, authId, token, refreshToken, actions, extraParams, redirect) =>
                        {
                            resource.AuthorizationId = authId;
                            resource.HeaderName = EastFive.Api.Configuration.SecurityDefinitions.AuthorizationHeader;
                            resource.Token = token;
                            resource.RefreshToken = refreshToken;
                            resource.ExtraParams = extraParams;
                            return request.CreateResponse(HttpStatusCode.Accepted, resource);
                        },
                        (location, why, paramsExtra) => request.CreateRedirectResponse(location).AddReason(why),
                        (why) => request.CreateResponse(HttpStatusCode.NotFound).AddReason(why),
                        () => request.CreateResponse(HttpStatusCode.Conflict).AddReason("User in token is not connected to this system"),
                        (why) => request.CreateResponse(HttpStatusCode.BadGateway).AddReason(why),
                        (why) => request.CreateResponseConfiguration(string.Empty, why),
                        (why) => request.CreateResponseUnexpectedFailure(why));
                    return session;
                },
                () => throw new NotImplementedException());
        }

        [HttpDelete]
        public static async Task<HttpResponseMessage> DeleteAsync(Resources.Queries.SessionQuery query,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return await query.ParseAsync(request,
                q => DeleteByIdAsync(q.Id.ParamSingle(), request, urlHelper));
        }

        private static async Task<HttpResponseMessage> DeleteByIdAsync(Guid sessionId, HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.Sessions.DeleteAsync(sessionId,
                    (type) => urlHelper.GetLocation(type),
                (sessionDeleted) =>
                {
                    if (null == sessionDeleted.logoutUrl || sessionDeleted.logoutUrl.AbsoluteUri.IsNullOrWhiteSpace())
                        return request.CreateResponse(HttpStatusCode.NoContent).AddReason("Logout Complete");

                    return request
                        .CreateResponse(
                            HttpStatusCode.Accepted, Convert(sessionDeleted, urlHelper))
                        .AddReason($"External session removal required:{sessionDeleted.logoutUrl.AbsoluteUri}");
                },
                () => request.CreateResponse(HttpStatusCode.NotFound),
                (why) => request.CreateResponse(HttpStatusCode.NotFound).AddReason(why));
        }
        
        [HttpOptions]
        public static HttpResponseMessage Options(
            [OptionalQueryParameter(Name = "id", CheckFileName = true)]Guid? id,
            ContentResponse onContent)
        {
            var post1 = new Resources.Session
            {
                Id = Guid.NewGuid(),
            };
            var response = new BlackBarLabs.Api.Resources.Options
            {
                Post = new[] { post1 },
            };
            if (!id.HasValue)
                return onContent(response);

            var authId = Guid.NewGuid();
            var jwtToken = BlackBarLabs.Security.Tokens.JwtTools.CreateToken(
                    id.Value, authId, new Uri("http://example.com"), TimeSpan.FromDays(200000),
                (token) => token,
                (config) => string.Empty,
                (config, why) => string.Empty);

            var put1 = new Resources.Session
            {
                Id = id.Value,
                AuthorizationId = authId,
            };
            response.Put = new[] { put1 };
            return onContent(response);
        }

        #endregion

    }
}
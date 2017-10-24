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

namespace EastFive.Security.SessionServer.Api
{
    public static class SessionActions
    {
        public static async Task<HttpResponseMessage> CreateAsync(this Api.Resources.Session resource,
            HttpRequestMessage request)
        {
            var responseSession = new Resources.Session()
            {
                Id = resource.Id,
            };
            
            //Get the session and Extrude it's information
            SessionServer.Sessions.CreateSessionSuccessDelegate<HttpResponseMessage> createSessionCallback =
                (authorizationId, token, refreshToken, extraParams) =>
                {
                    responseSession.AuthorizationId = authorizationId.Value;
                    responseSession.SessionHeader = new Resources.AuthHeaderProps { Name = "Authorization", Value = "Bearer " + token };
                    responseSession.RefreshToken = refreshToken;
                    return request.CreateResponse(HttpStatusCode.Created, responseSession);
                };

            var context = request.GetSessionServerContext();
            try
            {
                if (!resource.IsCredentialsPopulated())
                {
                    return await context.Sessions.CreateAsync(resource.Id,
                        createSessionCallback,
                        () => request.CreateResponse(HttpStatusCode.Conflict).AddReason("This session has already been created."),
                        (why) => request.CreateResponse(HttpStatusCode.InternalServerError).AddReason(why));
                }

                return await context.Sessions.CreateAsync(resource.Id,
                    resource.CredentialToken.Method, resource.CredentialToken.Token,
                    createSessionCallback,
                    () => request.CreateResponse(HttpStatusCode.Conflict).AddReason("This session has already been created."),
                    (why) => request.CreateResponse(HttpStatusCode.Conflict).AddReason($"Invalid credential in token:{why}"),
                    () => request.CreateResponse(HttpStatusCode.Conflict).AddReason("Account associated with that token is not associated with this system"),
                    () => request.CreateResponse(HttpStatusCode.Conflict).AddReason("Account associated with that token is not associated with a user in this system"),
                    (why) => request.CreateResponse(HttpStatusCode.BadGateway).AddReason(why));

            } catch(Exception ex)
            {
                return request.CreateResponse(HttpStatusCode.Conflict, ex.StackTrace);
            }
        }

        public static async Task<HttpResponseMessage> UpdateAsync(this Api.Resources.Session resource,
            HttpRequestMessage request)
        {
            if (!resource.IsCredentialsPopulated())
            {
                return request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason("Invalid credentials");
            }

            var context = request.GetSessionServerContext();
            // Can't update a session that does not exist
            var session = await context.Sessions.AuthenticateAsync(resource.Id,
                resource.CredentialToken.Method, resource.CredentialToken.Token,
                (authId, token, refreshToken, extraParams) =>
                {
                    resource.AuthorizationId = authId;
                    resource.SessionHeader = new Resources.AuthHeaderProps { Name = "Authorization", Value = token };
                    resource.RefreshToken = refreshToken;
                    return request.CreateResponse(HttpStatusCode.Accepted, resource);
                },
                (why) => request.CreateResponse(HttpStatusCode.NotFound).AddReason(why),
                () => request.CreateResponse(HttpStatusCode.Conflict).AddReason(
                    "Session is already authenticated. Please create a new session to repeat authorization."),
                () => request.CreateResponse(HttpStatusCode.Conflict).AddReason("User in token is not connected to this system"),
                (errorMessage) => request.CreateErrorResponse(HttpStatusCode.NotFound, errorMessage),
                (why) =>request.CreateResponse(HttpStatusCode.BadGateway));
            return session;
        }
    }
}
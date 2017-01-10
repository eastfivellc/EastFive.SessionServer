using BlackBarLabs.Api;
using BlackBarLabs.Security.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace BlackBarLabs.Security.SessionServer.Api
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
            AuthorizationServer.Sessions.CreateSessionSuccessDelegate<HttpResponseMessage> createSessionCallback =
                (authorizationId, token, refreshToken) =>
                {
                    responseSession.AuthorizationId = authorizationId;
                    responseSession.SessionHeader = new AuthHeaderProps { Name = "Authorization", Value = "Bearer " + token };
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
                        () => request.CreateResponse(HttpStatusCode.Conflict).AddReason("This session has already been created."));
                }

                return await context.Sessions.CreateAsync(resource.Id,
                    resource.Credentials.Method, resource.Credentials.Provider, resource.Credentials.UserId, resource.Credentials.Token,
                    createSessionCallback,
                    () => resource.Request.CreateResponse(HttpStatusCode.Conflict, "This session has already been created."),
                    (message) => resource.Request.CreateResponse(HttpStatusCode.Conflict, message));

            } catch(Exception ex)
            {
                return request.CreateResponse(HttpStatusCode.Conflict, ex.StackTrace);
            }
        }
    }
}
using BlackBarLabs.Api;
using BlackBarLabs.Security.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace BlackBarLabs.Security.AuthorizationServer.API.Resources
{
    public class SessionPost : Session, IHttpActionResult
    {
        public async Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var responseSession = new Session()
            {
                Id = this.Id,
            };

            //Get the session and Extrude it's information
            Sessions.CreateSessionSuccessDelegate<HttpResponseMessage> createSessionCallback = (authorizationId, token, refreshToken) =>
            {
                responseSession.AuthorizationId = authorizationId;
                responseSession.SessionHeader = new AuthHeaderProps { Name = "Authorization", Value = "Bearer " + token };
                responseSession.RefreshToken = refreshToken;
                return this.Request.CreateResponse(HttpStatusCode.Created, responseSession);
            };

            try
            {

                if (!this.IsCredentialsPopulated())
                {
                    return await this.Context.Sessions.CreateAsync(Id,
                        createSessionCallback,
                        () => this.Request.CreateResponse(HttpStatusCode.Conflict, "This session has already been created."));
                }

                return await this.Context.Sessions.CreateAsync(Id,
                    this.Credentials.Method, this.Credentials.Provider, this.Credentials.UserId, this.Credentials.Token,
                    createSessionCallback,
                    () => this.Request.CreateResponse(HttpStatusCode.Conflict, "This session has already been created."),
                    (message) => this.Request.CreateResponse(HttpStatusCode.Conflict, message));

            } catch(Exception ex)
            {
                return this.Request.CreateResponse(HttpStatusCode.Conflict, ex.StackTrace);
            }
        }
    }
}
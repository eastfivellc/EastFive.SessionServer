using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

using BlackBarLabs.Api;
using BlackBarLabs.Security.Session;

namespace BlackBarLabs.Security.SessionServer.Api.Resources
{
    public class SessionPut : Session, IHttpActionResult
    {
        public async Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            if (!this.IsCredentialsPopulated())
            {
                this.Request.CreateErrorResponse(HttpStatusCode.Conflict,
                    new Exception("Invalid credentials"));
            }
            
            // Can't update a session that does not exist
            var session = await this.Context.Sessions.AuthenticateAsync(this.Id, 
                this.Credentials.Method, this.Credentials.Provider,
                this.Credentials.UserId, this.Credentials.Token,
                (authId, token, refreshToken) =>
                {
                    this.AuthorizationId = authId;
                    this.SessionHeader = new AuthHeaderProps { Name = "Authorization", Value = token };
                    this.RefreshToken = refreshToken;
                    return this.Request.CreateResponse(HttpStatusCode.Accepted, this);
                },
                () =>
                {
                    return this.Request.CreateErrorResponse(HttpStatusCode.NotFound, new Exception("Invalid credentials"));
                },
                () =>
                {
                    return this.Request.CreateErrorResponse(HttpStatusCode.Conflict,
                        new ArgumentException("Session is already authenticated. Please create a new session to repeat authorization."));
                },
                (errorMessage) =>
                {
                    return this.Request.CreateErrorResponse(HttpStatusCode.NotFound, errorMessage);
                });
            return session;
        }
    }
}
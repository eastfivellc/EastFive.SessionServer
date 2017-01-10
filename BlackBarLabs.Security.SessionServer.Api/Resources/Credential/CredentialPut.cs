using System;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using BlackBarLabs.Security.AuthorizationServer.API.Models;
using System.Web.Http;
using System.Net.Http;
using System.Threading;
using BlackBarLabs.Api;
using BlackBarLabs.Security.Session;

namespace BlackBarLabs.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class CredentialPut : Credential, IHttpActionResult
    {
        #region Actionables
        
        public async Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var creationResults = await Context.Authorizations.UpdateCredentialsAsync(AuthorizationId,
                this.Method, this.Provider, this.UserId, this.Token,
                () => Request.CreateResponse(HttpStatusCode.Created, this),
                () => Request.CreateErrorResponse(HttpStatusCode.Conflict, "Authorization does not exist"),
                () => Request.CreateErrorResponse(HttpStatusCode.Conflict, "Update failed"));
            return creationResults;
        }

        #endregion
    }
}

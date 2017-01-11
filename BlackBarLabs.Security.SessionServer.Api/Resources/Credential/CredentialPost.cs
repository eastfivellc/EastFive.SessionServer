using System;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http;
using System.Net.Http;
using System.Threading;

using BlackBarLabs.Api;
using BlackBarLabs.Security.Session;
using BlackBarLabs.Security.AuthorizationServer.API.Models;

namespace BlackBarLabs.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class CredentialPost : Credential, IHttpActionResult
    {
        #region Actionables
        
        public async Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var creationResults = await Context.Authorizations.CreateCredentialsAsync(AuthorizationId,
                this.Method, this.Provider, this.UserId, this.Token,
                () => Request.CreateResponse(HttpStatusCode.Created, this),
                (why) => Request.CreateResponse(HttpStatusCode.Conflict).AddReason($"Authentication failed:{why}"),
                () => Request.CreateResponse(HttpStatusCode.Conflict).AddReason("Authorization does not exist"),
                (alreadyAssociatedAuthId) =>
                {
                    var alreadyAssociatedAuthIdUrl = (string)"";
                    return Request.CreateResponse(HttpStatusCode.Conflict, alreadyAssociatedAuthIdUrl);
                });
            return creationResults;
        }

        #endregion
    }
}

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
                () => Request.CreateResponse(HttpStatusCode.Conflict).AddReason("Authorization does not exist"),
                (why) => Request.CreateResponse(HttpStatusCode.Conflict).AddReason($"Update failed:{why}"));
            return creationResults;
        }

        #endregion
    }
}

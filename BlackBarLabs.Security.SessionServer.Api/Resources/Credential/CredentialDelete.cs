using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Net.Http;
using System.Threading;

namespace BlackBarLabs.Security.AuthorizationServer.API.Resources
{
    public class CredentialDelete : Credential, IHttpActionResult
    {
        #region Actionables
        
        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "Deleting Credential Resource is not yet supported"));
        }

        #endregion
    }
}

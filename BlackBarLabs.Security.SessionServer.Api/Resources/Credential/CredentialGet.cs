using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Net.Http;
using System.Threading;

namespace BlackBarLabs.Security.SessionServer.Api.Resources
{
    public class CredentialGet : Credential, IHttpActionResult
    {
        #region Actionables
        
        public async Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            return await this.Context.Authorizations.GetCredentialsAsync(
                this.Method, this.Provider, this.UserId, 
                (id) => Request.CreateResponse(HttpStatusCode.OK, id), 
                () => Request.CreateResponse(HttpStatusCode.NotFound));
        }

        #endregion
    }
}

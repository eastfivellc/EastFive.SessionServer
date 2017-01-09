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

namespace BlackBarLabs.Security.AuthorizationServer.API.Resources
{
    [DataContract]
    public class AuthorizationPost : Authorization, IHttpActionResult
    {
        #region Actionables
        
        public async Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var response = await this.Context.Authorizations.CreateAsync(this.Id,
                () => Request.CreateResponse(HttpStatusCode.Created),
                () => Request.CreateErrorResponse(HttpStatusCode.Conflict, "Authorization already exists"));
            return response;
        }

        #endregion
    }
}

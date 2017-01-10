using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace BlackBarLabs.Security.SessionServer.Api.Resources
{
    public class ClaimPost : Claim, IHttpActionResult
    {
        public async Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            return await this.Context.Claims.CreateAsync(Id,
                this.AuthorizationId, this.Issuer, this.Type, this.Value, this.Signature,
                () => this.Request.CreateResponse(HttpStatusCode.Created),
                () => this.Request.CreateResponse(HttpStatusCode.Conflict, "Authorization does not exists"),
                () => this.Request.CreateResponse(HttpStatusCode.Conflict, "Claim already exists for that authorization"),
                (message) => this.Request.CreateResponse(HttpStatusCode.Conflict, new Exception(message)));
        }
    }
}
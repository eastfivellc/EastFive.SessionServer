using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace BlackBarLabs.Security.AuthorizationServer.API.Resources
{
    public class ClaimPut : Claim, IHttpActionResult
    {
        public async Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            return await this.Context.Claims.UpdateAsync(Id,
                this.AuthorizationId, this.Issuer, this.Type, this.Value, this.Signature,
                () => this.Request.CreateResponse(HttpStatusCode.Created),
                () => this.Request.CreateResponse(HttpStatusCode.Conflict, "Authorization does not exists"),
                () => this.Request.CreateResponse(HttpStatusCode.Conflict, "Claim not found for that authorization"),
                (message) => this.Request.CreateResponse(HttpStatusCode.Conflict, new Exception(message)));
        }
    }
}
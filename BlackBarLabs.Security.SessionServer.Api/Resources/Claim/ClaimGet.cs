using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

using BlackBarLabs.Collections.Async;

namespace BlackBarLabs.Security.AuthorizationServer.API.Resources
{
    public class ClaimGet : Claim, IHttpActionResult
    {
        public async Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            return await this.Context.Claims.FindAsync(this.AuthorizationId, this.Type,
                (claimsAsync) =>
                {
                    var claims = claimsAsync.ToEnumerable((Guid claimId, Guid authorizationId, Uri type, string value) => new Claim()
                    {
                        Id = claimId,
                        AuthorizationId = authorizationId,
                        Type = type,
                        Value = value,
                    });
                    return this.Request.CreateResponse(HttpStatusCode.OK, claims.ToArray());
                },
                () => this.Request.CreateResponse(HttpStatusCode.NotFound, "Authorization does not exists"),
                (message) => this.Request.CreateResponse(HttpStatusCode.Conflict, message));
        }
    }
}
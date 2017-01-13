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
    public class SessionGet : Session, IHttpActionResult
    {
        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Request.CreateErrorResponse(HttpStatusCode.Unauthorized,
                "Get not permitted on Session Resource"));
        }
    }
}
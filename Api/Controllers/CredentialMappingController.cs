using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

using BlackBarLabs.Api;

using EastFive.Security.SessionServer.Api.Resources;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class CredentialMappingController : BaseController
    {
        public IHttpActionResult Post([FromBody]Resources.CredentialMapping resource)
        {
            return new HttpActionResult(() => this.CreateAsync(resource));
        }

        private async Task<HttpResponseMessage> CreateAsync(Resources.CredentialMapping resource)
        {
            var credentialMappingId = resource.Id.ToGuid();
            if (!credentialMappingId.HasValue)
                return this.Request.CreateResponse(HttpStatusCode.BadRequest)
                    .AddReason("Cannot create resource without Id");
            var actorId = resource.ActorId.ToGuid();
            if (!actorId.HasValue)
                return this.Request.CreateResponse(HttpStatusCode.BadRequest)
                    .AddReason("Actor Id is required");

            var claims = new System.Security.Claims.Claim[] { };
            var context = this.Request.GetSessionServerContext();
            var creationResults = await context.CredentialMappings.CreateAsync(credentialMappingId.Value,
                actorId.Value, resource.LoginId.ToGuid(),
                claims.ToArray(),
                () => this.Request.CreateResponse(HttpStatusCode.Created),
                () => this.Request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Mapping already exists"),
                () => this.Request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Login already exists"));
            return creationResults;
        }
    }
}


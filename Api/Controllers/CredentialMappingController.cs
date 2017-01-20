using BlackBarLabs.Api;
using EastFive.Security.SessionServer.Api.Resources;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

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
            var claims = new System.Security.Claims.Claim[] { };
            var context = this.Request.GetSessionServerContext();
            var creationResults = await context.Authorizations.CreateCredentialMappingAsync(resource.Id.UUID,
                resource.AuthorizationId,
                claims.ToArray(),
                (redirectId) => url.GetLocation<Controllers.InviteController>(redirectId),
                () => request.CreateResponse(HttpStatusCode.Created),
                (why) => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Authentication failed:{why}"),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"The resource already exists"),
                (why) => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason(why));
            return creationResults;
        }
    }
}


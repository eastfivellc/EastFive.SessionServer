using BlackBarLabs.Api;
using EastFive.Security.SessionServer.Api.Resources;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Routing;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class PasswordCredentialController : BaseController
    {
        public IHttpActionResult Post([FromBody]Resources.PasswordCredential model)
        {
            return new HttpActionResult(() => CreateAsync(model, this.Request, this.Url));
        }

        public async Task<HttpResponseMessage> CreateAsync(Resources.PasswordCredential credential,
            HttpRequestMessage request, UrlHelper url)
        {
            var credentialMappingId = credential.CredentialMappingId.ToGuid();
            //return await request.GetClaims(
            //    async (claims) =>
            //    {
            var claims = new System.Security.Claims.Claim[] { };
            var context = request.GetSessionServerContext();
            var creationResults = await context.CredentialMappings.CreatePasswordCredentialsAsync(
                credential.Id.UUID, credentialMappingId.Value,
                credential.UserId, credential.IsEmail, credential.Token, credential.ForceChange,
                claims.ToArray(),
                () => request.CreateResponse(HttpStatusCode.Created),
                (why) => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Authentication failed:{why}"),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Credential already exists"),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Credential mapping not found"),
                () => request.CreateResponse(HttpStatusCode.ServiceUnavailable),
                (why) => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason(why));
            return creationResults;
            //},
            //() => request.CreateResponse(HttpStatusCode.Unauthorized).ToTask(),
            //(why) => request.CreateResponse(HttpStatusCode.InternalServerError).AddReason(why).ToTask());
        }

        public IHttpActionResult Put([FromBody]PasswordCredential model)
        {
            return new HttpActionResult(() => model.PutAsync(this.Request));
        }

        public IHttpActionResult Delete([FromBody]Resources.Queries.CredentialQuery model)
        {
            return new HttpActionResult(() => model.DeleteAsync(this.Request));
        }

        public IHttpActionResult Get([FromUri]Resources.Queries.CredentialQuery model)
        {
            return new HttpActionResult(() => model.QueryAsync(this.Request));
        }

        public IHttpActionResult Options()
        {
            return new HttpActionResult(() => this.Request.CredentialOptionsAsync());
        }
    }
}


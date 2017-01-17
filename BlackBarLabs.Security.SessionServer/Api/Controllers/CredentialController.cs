using BlackBarLabs.Api;
using EastFive.Security.SessionServer.Api.Resources;
using System.Web.Http;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class CredentialController : BlackBarLabs.Api.Controllers.BaseController
    {
        public IHttpActionResult Post([FromBody]Resources.Credential model)
        {
            return new HttpActionResult(() => model.CreateAsync(this.Request));
        }

        public IHttpActionResult Put([FromBody]Credential model)
        {
            return new HttpActionResult(() => model.PutAsync(this.Request));
        }

        public IHttpActionResult Delete([FromBody]Resources.Queries.Credential model)
        {
            return new HttpActionResult(() => model.DeleteAsync(this.Request));
        }

        public IHttpActionResult Get([FromUri]Resources.Queries.Credential model)
        {
            return new HttpActionResult(() => model.QueryAsync(this.Request));
        }

        public IHttpActionResult Options()
        {
            return new HttpActionResult(() => this.Request.CredentialOptionsAsync());
        }
    }
}


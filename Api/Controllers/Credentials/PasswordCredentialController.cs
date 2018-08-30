using BlackBarLabs.Api;
using EastFive.Api.Azure.Controllers;
using EastFive.Security.SessionServer.Api;
using EastFive.Security.SessionServer.Api.Resources;
using EastFive.Security.SessionServer.Api.Resources.Queries;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Routing;

namespace EastFive.Api.Azure.Credentials.Controllers
{
    [RoutePrefix("aadb2c")]
    public class PasswordCredentialController : BaseController
    {
        public IHttpActionResult Post([FromBody]Security.SessionServer.Api.Resources.PasswordCredential model)
        {
            return new HttpActionResult(() => model.CreateAsync(this.Request, this.Url));
        }
        
        public IHttpActionResult Put([FromBody]Security.SessionServer.Api.Resources.PasswordCredential model)
        {
            return new HttpActionResult(() => model.PutAsync(this.Request, this.Url));
        }

        public IHttpActionResult Delete([FromBody]Security.SessionServer.Api.Resources.Queries.PasswordCredentialQuery model)
        {
            return new HttpActionResult(() => model.DeleteAsync(this.Request, this.Url));
        }
        
        public IHttpActionResult Get([FromUri]PasswordCredentialQuery model)
        {
            return new HttpActionResult(() => model.QueryAsync(this.Request, this.Url));
        }

        public IHttpActionResult Options()
        {
            return new HttpActionResult(() => this.Request.CredentialOptionsAsync());
        }
    }
}


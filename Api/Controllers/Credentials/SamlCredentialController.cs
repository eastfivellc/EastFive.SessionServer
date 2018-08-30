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
    [RoutePrefix("aadb2c")]
    public class SamlCredentialController : EastFive.Api.Azure.Controllers.BaseController
    {
        public IHttpActionResult Post([FromBody]Resources.SamlCredential model)
        {
            return new HttpActionResult(() => model.CreateAsync(this.Request, this.Url));
        }
        
        public IHttpActionResult Put([FromBody]Resources.SamlCredential model)
        {
            return new HttpActionResult(() => model.PutAsync(this.Request, this.Url));
        }

        public IHttpActionResult Delete([FromBody]Resources.Queries.SamlCredentialQuery model)
        {
            return new HttpActionResult(() => model.DeleteAsync(this.Request, this.Url));
        }
        
        public IHttpActionResult Get([FromUri]Resources.Queries.SamlCredentialQuery model)
        {
            return new HttpActionResult(() => model.QueryAsync(this.Request, this.Url));
        }
    }
}


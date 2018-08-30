using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Routing;

using BlackBarLabs.Api;
using EastFive.Api.Azure.Controllers;
using EastFive.Security.SessionServer.Api.Resources;

namespace EastFive.Api.Azure.Credentials.Controllers
{
    [RoutePrefix("aadb2c")]
    public class AuthenticationRequestLinkController : BaseController
    {   
        public IHttpActionResult Get([FromUri]Resources.Queries.AuthenticationRequestLinkQuery model)
        {
            return new HttpActionResult(() => model.QueryAsync(this.Request, this.Url));
        }
    }
}

using System;
using System.Threading.Tasks;
using System.Web.Http;
using BlackBarLabs.Api;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class TokenController : BaseController
    {
        public IHttpActionResult Get([FromUri]Resources.TokenQuery query)
        {
            return this.ActionResult(() => query.GetAsync(this.Request, this.Url));
        }
    }
}

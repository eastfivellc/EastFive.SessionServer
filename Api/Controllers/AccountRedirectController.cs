using BlackBarLabs.Api;
using EastFive.Security.SessionServer.Api.Resources;
using System.Web.Http;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    [RoutePrefix("aadb2c")]
    public class AccountRedirectController : BaseController
    {
        public IHttpActionResult Get([FromUri]Resources.Queries.AccountRedirectQuery model)
        {
            return new HttpActionResult(() => model.QueryAsync(this.Request, this.Url));
        }
    }
}


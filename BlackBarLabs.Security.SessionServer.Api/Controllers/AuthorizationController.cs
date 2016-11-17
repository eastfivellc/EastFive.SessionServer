using System.Web.Http;

namespace BlackBarLabs.Security.AuthorizationServer.API.Controllers
{
    public class AuthorizationController : BaseController
    {
        // POST: api/Order
        public IHttpActionResult Post([FromBody]Resources.AuthorizationPost model)
        {
            model.Request = Request;
            return model;
        }
        
        public IHttpActionResult Options()
        {
            var model = new Resources.AuthorizationOptions();
            return model;
        }
    }
}


using BlackBarLabs.Api;
using BlackBarLabs.Security.AuthorizationServer.API.Models;
using System.Threading.Tasks;
using System.Web.Http;

namespace BlackBarLabs.Security.AuthorizationServer.API.Controllers
{
    public class AuthorizeController : BaseController
    {
        // POST: api/Order
        public async Task<IHttpActionResult> Post([FromBody]Resources.Authorize model)
        {
            return (await model.PostAsync(Request)).ToActionResult();
        }
        
        public IHttpActionResult Options()
        {
            var model = new Resources.AuthorizationOptions();
            return model;
        }
    }
}


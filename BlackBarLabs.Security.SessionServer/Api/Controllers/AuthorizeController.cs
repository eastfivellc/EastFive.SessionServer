using BlackBarLabs.Api;
using System.Threading.Tasks;
using System.Web.Http;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class AuthorizeController : BlackBarLabs.Api.Controllers.BaseController
    {
        // POST: api/Order
        public async Task<IHttpActionResult> Post([FromBody]Resources.Authorize model)
        {
            return (await model.PostAsync(Request)).ToActionResult();
        }
    }
}


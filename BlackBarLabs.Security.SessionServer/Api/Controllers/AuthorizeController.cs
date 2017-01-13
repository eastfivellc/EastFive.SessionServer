using BlackBarLabs.Api;
using System.Threading.Tasks;
using System.Web.Http;

namespace BlackBarLabs.Security.SessionServer.Api.Controllers
{
    public class AuthorizeController : BaseController
    {
        // POST: api/Order
        public async Task<IHttpActionResult> Post([FromBody]Resources.Authorize model)
        {
            return (await model.PostAsync(Request)).ToActionResult();
        }
    }
}


using BlackBarLabs.Api;
using System.Threading.Tasks;
using System.Web.Http;

namespace BlackBarLabs.Security.SessionServer.Api.Controllers
{
    [RoutePrefix("session")]
    public class AuthorizationController : BaseController
    {
        // POST: api/Order
        public async Task<IHttpActionResult> Post([FromBody]Resources.Authorization model)
        {
            return (await model.CreateAsync(this.Request)).ToActionResult();
        }
        
        public async Task<IHttpActionResult> Options()
        {
            return (await this.Request.OptionsAsync()).ToActionResult();
        }
    }
}


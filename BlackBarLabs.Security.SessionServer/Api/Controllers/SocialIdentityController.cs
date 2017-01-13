using System.Threading.Tasks;
using System.Web.Mvc;
using NC2.CPM.Web.Utilities.Extensions;
using NC2.Security.AuthorizationServer.API.Models;
using NC2.Security.AuthorizationServer.API.Models.Filters;

namespace NC2.Security.AuthorizationServer.API.Controllers
{
    [System.Web.Mvc.Authorize]
    public class SocialIdentityController : BaseController
    {
        [System.Web.Mvc.AcceptVerbs(HttpVerbs.Post | HttpVerbs.Put | HttpVerbs.Get), System.Web.Mvc.Authorize]
        public async Task<ActionResult> Index(SocialIdentityModel model)
        {
            var viewModel = await model.ResolveAsync(DataContext, Request.GetHttpVerb(), Url, ControllerContext.RequestContext).ConfigureAwait(false);
            return View(viewModel);
        }

    }
}



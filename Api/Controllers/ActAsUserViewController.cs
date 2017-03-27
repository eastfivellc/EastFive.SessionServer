using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class ActAsUserViewController : Controller
    {
        public async Task<ActionResult> Index(string redirectUri, string token)
        {


            return View("~/Views/ActAsUser/");
        }

    }
}

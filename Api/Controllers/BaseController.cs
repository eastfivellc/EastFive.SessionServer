using EastFive.Api.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Controllers;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class BaseController : BlackBarLabs.Api.Controllers.BaseController
    {
        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);
        }
    }
}

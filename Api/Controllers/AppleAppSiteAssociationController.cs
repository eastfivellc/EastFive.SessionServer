using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class AppleAppSiteAssociationController : ApiController
    {
        public IHttpActionResult Get()
        {
            var content = new
            {
                applinks = new
                {
                    apps = new string[] { },
                    details = new object[]
                    {
                        new
                        {
                            appID = "W6R55DKE7X.com.eastfive.orderowl",
                            paths = new string [] { "*" },
                        }
                    }
                }
            };
            return this.ActionResult(() => this.Request.CreateResponse(HttpStatusCode.OK, content, Configuration.Formatters.JsonFormatter).ToTask());
        }
    }
}

//{
//    "applinks": {
//        "apps": [],
//        "details": [
//            {
//                "appID": "W6R55DKE7X.com.eastfive.orderowl",
//                "paths": [ "*"]
//            }
//        ]
//    }
//}

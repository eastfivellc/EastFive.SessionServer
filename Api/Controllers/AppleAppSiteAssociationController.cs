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
            var response = EastFive.Web.Configuration.Settings.GetString(
                EastFive.Security.SessionServer.Configuration.AppSettings.AppleAppSiteAssociationId,
                (appId) =>
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
                                    appID = appId,
                                    paths = new string [] { "*" },
                                }
                            }
                        }
                    };
                    return this.Request.CreateResponse(HttpStatusCode.OK, 
                        content, Configuration.Formatters.JsonFormatter);
                },
                (why) => this.Request.CreateResponse(HttpStatusCode.NotFound)
                    .AddReason(why));
            
            return this.ActionResult(() => response.ToTask());
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

using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using EastFive.Api;
using EastFive.Extensions;
using EastFive.Web.Configuration;
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
            var response = SessionServer.Configuration.AppSettings.AppleAppSiteAssociationId.ConfigurationString(
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
            
            return this.ActionResult(() => response.AsTask());
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

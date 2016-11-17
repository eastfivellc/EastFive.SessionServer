using System.Web.Http;
using System.Web.Mvc;
//using System.Web.Mvc;
//using System.Web.Optimization;
using System.Web.Routing;

namespace BlackBarLabs.Security.AuthorizationServer.API
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register); //This is if we're using Web.Api
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            //BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }
}

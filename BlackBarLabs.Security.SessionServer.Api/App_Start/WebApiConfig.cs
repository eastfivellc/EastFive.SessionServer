using BlackBarLabs.Security.CredentialProvider.AzureADB2C;
using System.Web.Http;
//using Microsoft.Owin.Security.OAuth;

namespace BlackBarLabs.Security.AuthorizationServer.API
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services
            // Configure Web API to use only bearer token authentication.
            //config.SuppressDefaultHostAuthentication();
            //config.Filters.Add(new HostAuthenticationFilter(OAuthDefaults.AuthenticationType));

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "Default",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            var x = config.AzureADB2CStartAsync("51d61cbc-d8bd-4928-8abb-6e1bb3155526", 
                new System.Uri("https://login.microsoftonline.com/humatestlogin.onmicrosoft.com/v2.0/.well-known/openid-configuration?p=B2C_1_signin1"),
                () => true,
                (why) => false);
        }
    }
}

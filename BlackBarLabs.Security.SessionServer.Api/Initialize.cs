using System;
using System.Linq;
using System.Net;
using System.Web.Http;
using System.Threading.Tasks;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

using Microsoft.IdentityModel.Tokens;

using BlackBarLabs.Extensions;
using BlackBarLabs.Web;
using BlackBarLabs.Api;
using BlackBarLabs.Security.CredentialProvider.AzureADB2C;

namespace BlackBarLabs.Security.SessionServer
{
    public static class ApiExtensions
    {
        public static TResult StartSessionServerAsync<TResult>(this HttpConfiguration config,
            Func<TResult> onSuccess,
            Func<string, TResult> onFailed)
        {
            AddExternalControllersX<Api.Controllers.AuthorizationController>(config);
            return config.AzureADB2CStartAsync("51d61cbc-d8bd-4928-8abb-6e1bb3155526",
                new System.Uri("https://login.microsoftonline.com/humatestlogin.onmicrosoft.com/v2.0/.well-known/openid-configuration?p=B2C_1_signin1"),
                onSuccess,
                onFailed);
        }

        public static void AddExternalControllersX<TController>(this HttpConfiguration config)
            where TController : ApiController
        {
            var routes = typeof(TController)
                .GetCustomAttributes<RoutePrefixAttribute>()
                .Select(routePrefix => routePrefix.Prefix)
                .Distinct();

            foreach (var routePrefix in routes)
            {
                config.Routes.MapHttpRoute(
                    name: routePrefix,
                    routeTemplate: routePrefix + "/{controller}/{id}",
                    defaults: new { id = RouteParameter.Optional });
            }

            //var assemblyRecognition = new InjectableAssemblyResolver(typeof(TController).Assembly,
            //    config.Services.GetAssembliesResolver());
            //config.Services.Replace(typeof(System.Web.Http.Dispatcher.IAssembliesResolver), assemblyRecognition);
        }
    }
}

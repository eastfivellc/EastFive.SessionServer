using System;
using System.Linq;
using System.Net;
using System.Configuration;
using System.Web.Http;
using System.Threading.Tasks;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

using Microsoft.IdentityModel.Tokens;

using BlackBarLabs;
using EastFive.Api.Services;
using BlackBarLabs.Api.Resources;
using System.Web.Http.Routing;

namespace EastFive.Security.SessionServer
{
    public static class Library
    {
        internal static Func<Guid, UrlHelper, WebId> getActorLink;

        public static TResult SessionServerStartAsync<TResult>(this HttpConfiguration config,
                Func<ISendMessageService> messageService,
                Func<Guid, UrlHelper, WebId> getActorLink,
            Func<TResult> onSuccess,
            Func<string, TResult> onFailed)
        {
            Library.getActorLink = getActorLink;
            Api.Controllers.BaseController.SetMessageService(messageService);
            //config.AddExternalControllers<SessionServer.Api.Controllers.OpenIdResponseController>();
            AddExternalControllersX<SessionServer.Api.Controllers.OpenIdResponseController>(config);
            //return InitializeAsync(audience, configurationEndpoint, onSuccess, onFailed);
            return onSuccess();
        }
        
        private static void AddExternalControllersX<TController>(HttpConfiguration config)
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

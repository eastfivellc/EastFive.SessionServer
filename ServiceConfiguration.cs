﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Collections.Concurrent;
using System.Web.Http.Routing;

using BlackBarLabs;
using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using BlackBarLabs.Linq;

namespace EastFive.Security.SessionServer
{
    public static class ServiceConfiguration
    {
        internal static Dictionary<CredentialValidationMethodTypes, IProvideAuthorization> credentialProviders =
            default(Dictionary<CredentialValidationMethodTypes, IProvideAuthorization>);

        internal static Dictionary<CredentialValidationMethodTypes, IProvideLogin> loginProviders =
            default(Dictionary<CredentialValidationMethodTypes, IProvideLogin>);

        internal static Dictionary<CredentialValidationMethodTypes, IProvideLoginManagement> managementProviders =
            default(Dictionary<CredentialValidationMethodTypes, IProvideLoginManagement>);

        internal static Dictionary<CredentialValidationMethodTypes, IProvideAccess> accessProviders =
            default(Dictionary<CredentialValidationMethodTypes, IProvideAccess>);

        public static async Task<TResult> InitializeAsync<TResult>(IConfigureIdentityServer configurationManager,
                HttpConfiguration config,
                Func<
                    Func<IProvideAuthorization, IProvideAuthorization[]>, // onProvideAuthorization
                    Func<IProvideAuthorization[]>, // onProvideNothing
                    Func<string, IProvideAuthorization[]>, // onFailure
                    Task<IProvideAuthorization[]>> [] initializers,
            Func<TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            Library.configurationManager = configurationManager;
            //config.AddExternalControllers<SessionServer.Api.Controllers.OpenIdResponseController>();
            AddExternalControllers<Api.Controllers.OpenIdResponseController>(config);
            //return InitializeAsync(audience, configurationEndpoint, onSuccess, onFailed);
            config.Routes.MapHttpRoute(name: "apple-app-links",
                routeTemplate: "apple-app-site-association",
                defaults: new { controller = "AppleAppSiteAssociation", id = RouteParameter.Optional });

            var dataContext = new EastFive.Security.SessionServer.Persistence.DataContext(Configuration.AppSettings.Storage);
            var credentialProvidersWithoutMethods = await initializers.Aggregate(
                (new IProvideAuthorization[] { }).ToTask(),
                async (providersTask, initializer) =>
                    {
                        var providers = await providersTask;
                        return await initializer(
                            (service) => providers.Append(service).ToArray(),
                            () => providers,
                            (why) => providers);
                    });
            credentialProviders = credentialProvidersWithoutMethods
                .ToDictionary(
                    credentialProvider => credentialProvider.Method,
                    credentialProvider => credentialProvider);
            loginProviders = credentialProvidersWithoutMethods
                .Where(credentialProvider => typeof(IProvideLogin).IsAssignableFrom(credentialProvider.GetType()))
                .ToDictionary(
                    credentialProvider => credentialProvider.Method,
                    credentialProvider => (IProvideLogin)credentialProvider);
            managementProviders = credentialProvidersWithoutMethods
                .Where(credentialProvider => typeof(IProvideLoginManagement).IsAssignableFrom(credentialProvider.GetType()))
                .ToDictionary(
                    credentialProvider => credentialProvider.Method,
                    credentialProvider => (IProvideLoginManagement)credentialProvider);
            accessProviders = credentialProvidersWithoutMethods
                .Where(credentialProvider => typeof(IProvideAccess).IsAssignableFrom(credentialProvider.GetType()))
                .ToDictionary(
                    credentialProvider => credentialProvider.Method,
                    credentialProvider => (IProvideAccess)credentialProvider);

            return onSuccess();
        }

        private static void AddExternalControllers<TController>(HttpConfiguration config)
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
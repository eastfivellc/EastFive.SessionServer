using System;
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
using EastFive.Linq;
using System.IO;
using System.IO.Compression;
using EastFive.Collections.Generic;
using System.Linq.Expressions;

namespace EastFive.Security.SessionServer
{
    public static class ServiceConfiguration
    {
        public delegate object IntegrationActivityDelegate(EastFive.Api.Azure.Integration integration,
            Func<object, Task<object>> unlockAsync,
            Func<string, Task<object>> unlockWithIssueAsync);

        public delegate EastFive.Azure.Synchronization.Connections SynchronizationActivityDelegate(EastFive.Azure.Synchronization.Synchronizations<object> synchronzation);

        internal static Dictionary<CredentialValidationMethodTypes, IProvideAuthorization> credentialProviders =
            default(Dictionary<CredentialValidationMethodTypes, IProvideAuthorization>);

        internal static Dictionary<string, IProvideLogin> loginProviders =
            default(Dictionary<string, IProvideLogin>);

        internal static Dictionary<CredentialValidationMethodTypes, IProvideLoginManagement> managementProviders =
            default(Dictionary<CredentialValidationMethodTypes, IProvideLoginManagement>);

        internal static Dictionary<CredentialValidationMethodTypes, IProvideToken> tokenProviders =
            default(Dictionary<CredentialValidationMethodTypes, IProvideToken>);

        internal static IDictionary<Type, IDictionary<string, IntegrationActivityDelegate[]>> integrationActivites =
            new Dictionary<Type, IDictionary<string, IntegrationActivityDelegate[]>>();
        
        public static async Task<TResult> InitializeAsync<TResult>(IConfigureIdentityServer configurationManager,
                HttpConfiguration config,
                Func<
                    Func<IProvideAuthorization, IProvideAuthorization[]>, // onProvideAuthorization
                    Func<IProvideAuthorization[]>, // onProvideNothing
                    Func<string, IProvideAuthorization[]>, // onFailure
                    Task<IProvideAuthorization[]>> [] initializers,
                Expression<IntegrationActivityDelegate>[] activities,
                Expression<SynchronizationActivityDelegate>[] synchronizations,
            Func<TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            Library.configurationManager = configurationManager;
            
            EastFive.Api.Modules.ControllerModule.AddInstigator(typeof(Context),
                (httpApp, request, parameterInfo, onCreatedSessionContext) => onCreatedSessionContext(new Context(
                    () => new Persistence.DataContext(Configuration.AppSettings.Storage))));
            
            AddExternalControllers<Api.Controllers.OpenIdResponseController>(config);
            config.Routes.MapHttpRoute(name: "apple-app-links",
                routeTemplate: "apple-app-site-association",
                defaults: new { controller = "AppleAppSiteAssociation", id = RouteParameter.Optional });

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
                    credentialProvider =>
                    {
                        var methodName = credentialProvider.GetType().GetCustomAttribute<Attributes.IntegrationNameAttribute>().Name;
                        Enum.TryParse(methodName, out CredentialValidationMethodTypes method);
                        return method;
                    },
                    credentialProvider => credentialProvider);
            loginProviders = credentialProvidersWithoutMethods
                .Where(credentialProvider => typeof(IProvideLogin).IsAssignableFrom(credentialProvider.GetType()))
                .ToDictionary(
                    credentialProvider => credentialProvider.GetType().GetCustomAttribute<Attributes.IntegrationNameAttribute>().Name,
                    credentialProvider => (IProvideLogin)credentialProvider);
            managementProviders = credentialProviders
                .Where(credentialProvider => typeof(IProvideLoginManagement).IsAssignableFrom(credentialProvider.Value.GetType()))
                .ToDictionary(
                    credentialProvider => credentialProvider.Key,
                    credentialProvider => (IProvideLoginManagement)credentialProvider.Value);
            tokenProviders = credentialProviders
                .Where(credentialProvider => typeof(IProvideToken).IsAssignableFrom(credentialProvider.Value.GetType()))
                .ToDictionary(
                    credentialProvider => credentialProvider.Key,
                    credentialProvider => (IProvideToken)credentialProvider.Value);

            //var spaZipPath = System.Web.Hosting.HostingEnvironment.MapPath("~/Spa.zip");
            //var zipArchive = ZipFile.OpenRead(spaZipPath);

            //var lookupSpaFile = zipArchive.Entries
            //    .Select(
            //        entity => entity.FullName.PairWithValue(entity.Open().ToBytes()))
            //    .ToDictionary();

            integrationActivites = activities
                .Where(activity => activity.Body is System.Linq.Expressions.MethodCallExpression)
                .SelectMany(
                    (activity) =>
                    {
                        var method = (activity.Body as System.Linq.Expressions.MethodCallExpression).Method;
                        return method.DeclaringType.GetInterfaces()
                            .FlatMap(
                                (conformsTo, nextItem, skipItem) => method.GetCustomAttribute(
                                    (Attributes.IntegrationNameAttribute integrationNameAttr) => nextItem(
                                        conformsTo.PairWithValue(integrationNameAttr.Name).PairWithValue(activity.Compile())),
                                    () => skipItem()),
                                (IEnumerable<KeyValuePair<KeyValuePair<Type, string>, IntegrationActivityDelegate>> activityKvps) => activityKvps);
                    })
                .GroupBy(activityKvp => activityKvp.Key.Key)
                .Select(
                    grp => grp.Key.PairWithValue(
                        (IDictionary<string, IntegrationActivityDelegate[]>)grp
                            .Select(item => item.Key.Value.PairWithValue(item.Value))
                            .GroupBy(kvp => kvp.Key)
                            .Select(grpInner => grpInner.Key.PairWithValue(grpInner.SelectValues().ToArray()))
                            .ToDictionary()))
                .ToDictionary();
            
            //var synchronizationsDictionary = synchronizations
            //    .Where(activity => activity.Body is System.Linq.Expressions.MethodCallExpression)
            //    .SelectMany(
            //        (activity) =>
            //        {
            //            var method = (activity.Body as System.Linq.Expressions.MethodCallExpression).Method;
            //            return method.DeclaringType.GetInterfaces()
            //                .FlatMap(
            //                    (conformsTo, nextItem, skipItem) => method.GetCustomAttribute(
            //                        (Attributes.IntegrationNameAttribute integrationNameAttr) => nextItem(
            //                            conformsTo.PairWithValue(integrationNameAttr.Name).PairWithValue(activity.Compile())),
            //                        () => skipItem()),
            //                    (IEnumerable<KeyValuePair<KeyValuePair<Type, string>, IntegrationActivityDelegate>> activityKvps) => activityKvps);
            //        })
            //    .GroupBy(activityKvp => activityKvp.Key.Key)
            //    .Select(
            //        grp => grp.Key.PairWithValue(
            //            (IDictionary<string, IntegrationActivityDelegate[]>)grp
            //                .Select(item => item.Key.Value.PairWithValue(item.Value))
            //                .GroupBy(kvp => kvp.Key)
            //                .Select(grpInner => grpInner.Key.PairWithValue(grpInner.SelectValues().ToArray()))
            //                .ToDictionary()))
            //    .ToDictionary();

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

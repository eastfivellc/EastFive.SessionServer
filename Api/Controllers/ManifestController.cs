using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System;

using BlackBarLabs.Api;
using EastFive.Api;
using EastFive;
using System.Web.Http.Routing;
using EastFive.Api.Controllers;
using BlackBarLabs.Extensions;
using EastFive.Linq;
using EastFive.Extensions;

namespace EastFive.Api.Azure.Controllers
{
    [FunctionViewController(Route = "Manifest")]
    public static class ManifestController
    {
        //public async Task<IHttpActionResult> Get(
        //    [FromUri]Resources.Manifest[] query)
        //{
        //    return await this.Request.GetPossibleMultipartResponseAsync(query,
        //        (manifest) => () => manifest.Query(Request, Url));
        //}

        [EastFive.Api.HttpGet]
        public static Task<HttpResponseMessage> FindByIdAsync(
                HttpRequestMessage request, UrlHelper url,
            ContentResponse onFound,
            NotFoundResponse onNotFound,
            UnauthorizedResponse onUnauthorized)
        {
            LocateControllers();
            var endpoints = ManifestController.lookup
                .Select(
                    type =>
                    {
                        var endpoint = url.GetWebId(type, "x-com.orderowl:ordering");
                        return endpoint;
                    })
                .ToArray();

            var manifest = new Resources.Manifest()
            {
                Id = Guid.NewGuid(),
                Endpoints = endpoints,
            };

            return request.CreateResponse(System.Net.HttpStatusCode.OK, manifest).ToTask();
        }


        #region Load Controllers

        private static object lookupLock = new object();
        private static Type[] lookup;

        private static void LocateControllers()
        {
            lock (lookupLock)
            {
                if (!ManifestController.lookup.IsDefaultNullOrEmpty())
                    return;

                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => (!assembly.GlobalAssemblyCache))
                    .ToArray();

                AppDomain.CurrentDomain.AssemblyLoad += (object sender, AssemblyLoadEventArgs args) =>
                {
                    lock (lookupLock)
                    {
                        AddControllersFromAssembly(args.LoadedAssembly);
                    }
                };

                foreach (var assembly in loadedAssemblies)
                {
                    AddControllersFromAssembly(assembly);
                }
            }
        }
        

        private static void AddControllersFromAssembly(System.Reflection.Assembly assembly)
        {
            try
            {
                var types = assembly
                    .GetTypes();
                var results = types
                    .Where(type =>
                        typeof(BlackBarLabs.Api.Controllers.BaseController).IsAssignableFrom(type) ||
                        typeof(EastFive.Api.Controllers.ApiController).IsAssignableFrom(type) ||
                        typeof(System.Web.Http.ApiController).IsAssignableFrom(type) ||
                        type.GetCustomAttribute<FunctionViewControllerAttribute, bool>((attrs) => true, () => false))
                    .ToArray();

                ManifestController.lookup = ManifestController.lookup.NullToEmpty().Concat(results).ToArray();
            }
            catch (Exception ex)
            {
                ex.GetType();
            }
        }

        #endregion
    }
}

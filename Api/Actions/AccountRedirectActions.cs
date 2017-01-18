using System;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http;
using System.Net.Http;
using System.Threading;

using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using System.Web.Http.Routing;
using BlackBarLabs;
using EastFive.Api.Services;

namespace EastFive.Security.SessionServer.Api
{
    public static class AccountRedirectActions
    {
        #region Actionables

        public static async Task<HttpResponseMessage> QueryAsync(this Resources.Queries.AccountRedirectQuery query,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return await query.ParseAsync(request,
                q => QueryById(q.Id.ParamSingle(), request, urlHelper));
        }

        private async static Task<HttpResponseMessage> QueryById(Guid redirectId, HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            var loginProviderTaskGetter = (Func<Task<IIdentityService>>)
                request.Properties[BlackBarLabs.Api.ServicePropertyDefinitions.IdentityService];
            var loginProviderTask = loginProviderTaskGetter();
            var loginProvider = await loginProviderTask;
            return await context.Authorizations.GetCredentialRedirectAsync(
                redirectId,
                (state) =>
                {
                    var callbackUrl = urlHelper.GetLocation<Controllers.OpenIdResponseController>(
                        typeof(Controllers.OpenIdResponseController)
                            .GetCustomAttributes<RoutePrefixAttribute>()
                            .Select(routePrefix => routePrefix.Prefix)
                            .First());

                    var redirect = loginProvider.GetSignupUrl(
                        "http://orderowl.com/Login", 1, state,
                        callbackUrl);
                    var response = request.CreateResponse(HttpStatusCode.Redirect);
                    response.Headers.Location = redirect;
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound).AddReason("Already used"),
                () => request.CreateResponse(HttpStatusCode.NotFound));
        }

        #endregion
    }
}

using BlackBarLabs.Api;
using EastFive.Security.SessionServer.Api.Resources;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Routing;
using BlackBarLabs;
using System;
using EastFive.Api.Services;
using BlackBarLabs.Extensions;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    [RoutePrefix("aadb2c")]
    public class InviteController : BaseController
    {
        #region Get

        public IHttpActionResult Get([FromUri]Resources.Queries.InviteQuery model)
        {
            return new HttpActionResult(() => QueryAsync(model, this.Request, this.Url));
        }
        public async Task<HttpResponseMessage> QueryAsync(Resources.Queries.InviteQuery query,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return await query.ParseAsync(request,
                q => QueryByIdAsync(q.Id.ParamSingle(), request, urlHelper),
                q => QueryByTokenAsync(q.Token.ParamSingle(), request, urlHelper));
        }

        private async Task<HttpResponseMessage> QueryByIdAsync(Guid inviteId, HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            var loginProviderTaskGetter = (Func<Task<IIdentityService>>)
                request.Properties[BlackBarLabs.Api.ServicePropertyDefinitions.IdentityService];
            var loginProviderTask = loginProviderTaskGetter();
            var loginProvider = await loginProviderTask;
            return await context.CredentialMappings.GetInviteAsync(inviteId,
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

        private async Task<HttpResponseMessage> QueryByTokenAsync(Guid token, HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            var loginProviderTaskGetter = (Func<Task<IIdentityService>>)
                request.Properties[BlackBarLabs.Api.ServicePropertyDefinitions.IdentityService];
            var loginProviderTask = loginProviderTaskGetter();
            var loginProvider = await loginProviderTask;
            return await await context.CredentialMappings.GetInviteByTokenAsync(token,
                (state) =>
                {
                    var callbackUrl = urlHelper.GetLocation<Controllers.OpenIdResponseController>();
                    var redirect = loginProvider.GetSignupUrl(
                        "http://orderowl.com/Login", 1, state,
                        callbackUrl);
                    var response = request.CreateResponse(HttpStatusCode.Redirect);
                    response.Headers.Location = redirect;
                    return response.ToTask();
                },
                (actorId) =>
                {
                    return context.Sessions.CreateAsync(Guid.NewGuid(), actorId,
                        new System.Security.Claims.Claim[] { },
                        (bearerToken, refreshToken) =>
                        {
                            var redirectUrl = new Uri("http://orderowl.com/Login")
                                .SetQueryParam("authoriationId", actorId.ToString("N"))
                                .SetQueryParam("token", bearerToken)
                                .SetQueryParam("refreshToken", refreshToken);
                            var response = request.CreateResponse(HttpStatusCode.Redirect);
                            response.Headers.Location = redirectUrl;
                            return response;
                        },
                        () => request.CreateResponse(418).AddReason("You are more unique than a GUID"));
                },
                () => request.CreateResponse(HttpStatusCode.NotFound).AddReason("Already used").ToTask(),
                () => request.CreateResponse(HttpStatusCode.NotFound).ToTask());
        }

        #endregion

        public IHttpActionResult Post([FromBody]Resources.Invite model)
        {
            return new HttpActionResult(() => CreateAsync(model, this.Request, this.Url));
        }

        public async Task<HttpResponseMessage> CreateAsync(Resources.Invite credential,
            HttpRequestMessage request, UrlHelper url)
        {
            var credentialMappingId = credential.CredentialMapping.ToGuid();
            //return await request.GetClaims(
            //    async (claims) =>
            //    {
            var claims = new System.Security.Claims.Claim[] { };
            var context = request.GetSessionServerContext();
            var creationResults = await context.CredentialMappings.SendEmailInviteAsync(
                credential.Id.UUID, credentialMappingId.Value, credential.Email,
                claims.ToArray(),
                (inviteId, token) => url.GetLocation<InviteController>().SetQueryParam("token", token.ToString("N")),
                () => request.CreateResponse(HttpStatusCode.Created),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Invite already exists"),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Credential mapping not found"),
                () => request.CreateResponse(HttpStatusCode.ServiceUnavailable),
                (why) => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason(why));
            return creationResults;
            //},
            //() => request.CreateResponse(HttpStatusCode.Unauthorized).ToTask(),
            //(why) => request.CreateResponse(HttpStatusCode.InternalServerError).AddReason(why).ToTask());
        }
    }
}


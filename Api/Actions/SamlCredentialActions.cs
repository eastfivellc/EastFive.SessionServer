using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Net.Http;
using System.Web.Http.Routing;
using System.Configuration;

using BlackBarLabs;
using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using EastFive.Api.Services;
using EastFive.Security.SessionServer.Configuration;
using EastFive.Api.Azure.Credentials.Controllers;

namespace EastFive.Security.SessionServer.Api
{
    public static class SamlCredentialActions
    {
        public static async Task<HttpResponseMessage> CreateAsync(this Resources.SamlCredential credential,
            HttpRequestMessage request, UrlHelper url)
        {
            return await request.GetActorIdClaimsAsync(
                async (performingActorId, claims) =>
                {
                    var response = await CreateSamlCredentialAsync(credential, request, url, performingActorId, claims);
                    return response;
                });
        }

        private static async Task<HttpResponseMessage> CreateSamlCredentialAsync(Resources.SamlCredential credential,
            HttpRequestMessage request, UrlHelper url,
            Guid performingActorId, System.Security.Claims.Claim[]claims)
        {
            var credentialId = credential.Id.ToGuid();
            if (!credentialId.HasValue)
                return request.CreateResponse(HttpStatusCode.BadRequest).AddReason("ID property is not set");
            var actorId = credential.Actor.ToGuid();
            if (!actorId.HasValue)
                return request.CreateResponse(HttpStatusCode.BadRequest).AddReason("Actor (a property) is not set");
            var userId = credential.UserId;
            if (String.IsNullOrWhiteSpace(userId))
                return request.CreateResponse(HttpStatusCode.BadRequest).AddReason("User (a property) is not set");

            var context = request.GetSessionServerContext();
            var creationResults = await context.Credentials.CreateSamlCredentialAsync(
                credentialId.Value, actorId.Value, userId,
                performingActorId, claims,
                () => request.CreateResponse(HttpStatusCode.Created),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Credential already exists"),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Username already in use"),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Relationship already exists"),
                () => request.CreateResponse(HttpStatusCode.Unauthorized),
                (why) => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason(why));
            return creationResults;
        }

        public static Task<HttpResponseMessage> PutAsync(this Resources.SamlCredential credential,
            HttpRequestMessage request, UrlHelper url)
        {
            throw new NotImplementedException();
            //return await request.GetActorIdClaimsAsync(ClaimsDefinitions.AccountIdClaimType,
            //    async (performingActorId, claims) =>
            //    {
            //        var loginProviderTaskGetter = (Func<Task<IIdentityService>>)
            //        request.Properties[ServicePropertyDefinitions.IdentityService];
            //        var loginProviderTask = loginProviderTaskGetter();
            //        var loginProvider = await loginProviderTask;
            //        var callbackUrl = url.GetLocation<Controllers.OpenIdResponseController>();
            //        var landingPage = Web.Configuration.Settings.Get(SessionServer.Configuration.AppSettings.LandingPage);
            //        var loginUrl = loginProvider.GetLoginUrl(landingPage, 0, new byte[] { }, callbackUrl);

            //        var context = request.GetSessionServerContext();
            //        var creationResults = await context.PasswordCredentials.UpdatePasswordCredentialAsync(credential.Id.UUID,
            //            credential.Token, credential.ForceChange, credential.LastEmailSent, loginUrl,
            //            performingActorId, claims,
            //            () => request.CreateResponse(HttpStatusCode.NoContent),
            //            () => request.CreateResponse(HttpStatusCode.NotFound),
            //            () => request.CreateResponse(HttpStatusCode.Unauthorized),
            //            () => request.CreateResponse(HttpStatusCode.ServiceUnavailable),
            //            (why) => request.CreateResponse(HttpStatusCode.Conflict).AddReason($"Update failed:{why}"));
            //        return creationResults;
            //    });
        }

        #region Actionables

        public static Task<HttpResponseMessage> QueryAsync(this Resources.Queries.SamlCredentialQuery credential,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return request.GetActorIdClaimsAsync(ClaimsDefinitions.AccountIdClaimType,
                (actorPerformingId, claims) => credential.ParseAsync(request,
                    q => QueryByIdAsync(q.Id.ParamSingle(), request, urlHelper, actorPerformingId, claims),
                    q => QueryByActorId(q.Actor.ParamSingle(), request, urlHelper, actorPerformingId, claims)));
        }

        private static async Task<HttpResponseMessage> QueryByIdAsync(Guid passwordCredentialId,
            HttpRequestMessage request, UrlHelper urlHelper,
            Guid actorPerformingId, System.Security.Claims.Claim [] claims)
        {
            var context = request.GetSessionServerContext();
            return await context.PasswordCredentials.GetPasswordCredentialAsync(passwordCredentialId,
                    actorPerformingId, claims,
                (passwordCredential) =>
                {
                    var response = request.CreateResponse(HttpStatusCode.OK,
                        Convert(passwordCredential, urlHelper));
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound),
                () => request.CreateResponse(HttpStatusCode.NotFound),
                (why) => request.CreateResponse(HttpStatusCode.NotFound));
        }

        private async static Task<HttpResponseMessage[]> QueryByActorId(Guid actorId,
            HttpRequestMessage request, UrlHelper urlHelper,
            Guid actorPerformingId, System.Security.Claims.Claim[] claims)
        {
            if (!await Library.configurationManager.CanAdministerCredentialAsync(actorId, actorPerformingId, claims))
                return request.CreateResponse(HttpStatusCode.NotFound).AsEnumerable().ToArray();

            var context = request.GetSessionServerContext();
            return await context.PasswordCredentials.GetPasswordCredentialByActorAsync(
                actorId,
                (credentials) => credentials.Select(
                    passwordCredential =>
                    {
                        var response = request.CreateResponse(HttpStatusCode.OK, 
                            Convert(passwordCredential, urlHelper));
                        return response;
                    }).ToArray(),
                () => request.CreateResponse(HttpStatusCode.NotFound).AsEnumerable().ToArray(),
                (why) => request.CreateResponse(HttpStatusCode.ServiceUnavailable).AddReason(why).AsEnumerable().ToArray());
        }

        private static Resources.SamlCredential Convert(EastFive.Api.Azure.Credentials.PasswordCredential passwordCredential, UrlHelper urlHelper)
        {
            return new Resources.SamlCredential
            {
                Id = urlHelper.GetWebId<PasswordCredentialController>(passwordCredential.id),
                Actor = passwordCredential.actorId,
                UserId = passwordCredential.userId,
            };
        }

        public static async Task<HttpResponseMessage> DeleteAsync(this Resources.Queries.SamlCredentialQuery credential,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return await credential.ParseAsync(request,
                q => DeleteByIdAsync(q.Id.ParamSingle(), request, urlHelper));
        }

        private static async Task<HttpResponseMessage> DeleteByIdAsync(Guid passwordCredentialId, HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.PasswordCredentials.DeletePasswordCredentialAsync(passwordCredentialId,
                () =>
                {
                    var response = request.CreateResponse(HttpStatusCode.NoContent);
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound),
                (why) => request.CreateResponse(HttpStatusCode.NotFound));
        }

        #endregion
    }
}

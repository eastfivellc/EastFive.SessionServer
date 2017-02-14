using System;
using System.IdentityModel.Protocols.WSTrust;
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
using EastFive.IdentityServer.Configuration;
using System.Configuration;

namespace EastFive.Security.SessionServer.Api
{
    public static class PasswordCredentialActions
    {
        public static async Task<HttpResponseMessage> CreateAsync(this Resources.PasswordCredential credential,
            HttpRequestMessage request, UrlHelper url)
        {
            return await request.GetActorIdClaimsAsync(ClaimsDefinitions.AccountIdClaimType,
                async (performingActorId, claims) =>
                {
                    var response = await CreatePasswordCredentialAsync(credential, request, url, performingActorId, claims);
                    return response;
                });
        }

        private static async Task<HttpResponseMessage> CreatePasswordCredentialAsync(Resources.PasswordCredential credential,
            HttpRequestMessage request, UrlHelper url,
            Guid performingActorId, System.Security.Claims.Claim[]claims)
        {
            var actorId = credential.Actor.ToGuid();
            var loginProviderTaskGetter = (Func<Task<IIdentityService>>)
                request.Properties[ServicePropertyDefinitions.IdentityService];
            var loginProviderTask = loginProviderTaskGetter();
            var loginProvider = await loginProviderTask;
            var callbackUrl = url.GetLocation<Controllers.OpenIdResponseController>();
            var landingPage = ConfigurationManager.AppSettings[EastFive.IdentityServer.Configuration.RouteDefinitions.LandingPage];
            var loginUrl = loginProvider.GetLoginUrl(landingPage, 0, new byte[] { }, callbackUrl);
            
            var context = request.GetSessionServerContext();
            var creationResults = await context.PasswordCredentials.CreatePasswordCredentialsAsync(
                credential.Id.UUID, actorId.Value,
                credential.UserId, credential.IsEmail, credential.Token, credential.ForceChange,
                credential.LastEmailSent, loginUrl,
                performingActorId, claims,
                () => request.CreateResponse(HttpStatusCode.Created),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Credential already exists"),
                (actorUsingId) => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Username already in use with Actor:{actorUsingId}"),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Password is insufficient."),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Relationship already exists"),
                () => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason($"Login is already in use"),
                () => request.CreateResponse(HttpStatusCode.Unauthorized),
                () => request.CreateResponse(HttpStatusCode.ServiceUnavailable),
                (why) => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason(why));
            return creationResults;
        }

        public static async Task<HttpResponseMessage> PutAsync(this Resources.PasswordCredential credential,
            HttpRequestMessage request, UrlHelper url)
        {
            return await request.GetActorIdClaimsAsync(ClaimsDefinitions.AccountIdClaimType,
                async (performingActorId, claims) =>
                {
                    var loginProviderTaskGetter = (Func<Task<IIdentityService>>)
                    request.Properties[ServicePropertyDefinitions.IdentityService];
                    var loginProviderTask = loginProviderTaskGetter();
                    var loginProvider = await loginProviderTask;
                    var callbackUrl = url.GetLocation<Controllers.OpenIdResponseController>();
                    var landingPage = ConfigurationManager.AppSettings[
                        EastFive.IdentityServer.Configuration.RouteDefinitions.LandingPage];
                    var loginUrl = loginProvider.GetLoginUrl(landingPage, 0, new byte[] { }, callbackUrl);

                    var context = request.GetSessionServerContext();
                    var creationResults = await context.PasswordCredentials.UpdatePasswordCredentialAsync(credential.Id.UUID,
                        credential.Token, credential.ForceChange, credential.LastEmailSent, loginUrl,
                        performingActorId, claims,
                        () => request.CreateResponse(HttpStatusCode.NoContent),
                        () => request.CreateResponse(HttpStatusCode.NotFound),
                        () => request.CreateResponse(HttpStatusCode.Unauthorized),
                        () => request.CreateResponse(HttpStatusCode.ServiceUnavailable),
                        (why) => request.CreateResponse(HttpStatusCode.Conflict).AddReason($"Update failed:{why}"));
                    return creationResults;
                });
        }

        #region Actionables

        public static Task<HttpResponseMessage> QueryAsync(this Resources.Queries.PasswordCredentialQuery credential,
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
            return await await context.PasswordCredentials.GetPasswordCredentialAsync(passwordCredentialId,
                async (passwordCredential) =>
                {
                    if (!await Library.configurationManager.CanAdministerCredentialAsync(passwordCredential.actorId, actorPerformingId, claims))
                        return request.CreateResponse(HttpStatusCode.NotFound);
                    var response = request.CreateResponse(HttpStatusCode.OK,
                        Convert(passwordCredential, urlHelper));
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound).ToTask(),
                (why) => request.CreateResponse(HttpStatusCode.NotFound).ToTask());
        }

        private async static Task<HttpResponseMessage[]> QueryByActorId(Guid actorId,
            HttpRequestMessage request, UrlHelper urlHelper,
            Guid actorPerformingId, System.Security.Claims.Claim[] claims)
        {
            if (!await Library.configurationManager.CanAdministerCredentialAsync(actorId, actorPerformingId, claims))
                return request.CreateResponse(HttpStatusCode.NotFound).ToEnumerable().ToArray();

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
                () => request.CreateResponse(HttpStatusCode.NotFound).ToEnumerable().ToArray(),
                (why) => request.CreateResponse(HttpStatusCode.ServiceUnavailable).AddReason(why).ToEnumerable().ToArray());
        }

        private static Resources.PasswordCredential Convert(PasswordCredential passwordCredential, UrlHelper urlHelper)
        {
            return new Resources.PasswordCredential
            {
                Id = urlHelper.GetWebId<Controllers.PasswordCredentialController>(passwordCredential.id),
                Actor = passwordCredential.actorId,
                UserId = passwordCredential.userId,
                IsEmail = passwordCredential.isEmail,
                ForceChange = passwordCredential.forceChangePassword,
                Token = String.Empty,
                LastEmailSent = passwordCredential.lastSent,
            };
        }

        public static async Task<HttpResponseMessage> DeleteAsync(this Resources.Queries.PasswordCredentialQuery credential,
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

        public static Task<HttpResponseMessage> CredentialOptionsAsync(this HttpRequestMessage request)
        {
            var credentialProviders = new Resources.PasswordCredential[]
            {
                new Resources.PasswordCredential
                {
                    UserId = "0123456789",
                    Token = "ABC.123.MXC",
                },
                new Resources.PasswordCredential
                {
                    //Method = CredentialValidationMethodTypes.OpenIdConnect,
                    //Provider = new Uri("urn:auth.gibbits.nc2media.com/AuthOpenIdConnect/"),
                    UserId = Guid.NewGuid().ToString("N"),
                    Token = "EDF.123.A3EF",
                },
                new Resources.PasswordCredential
                {
                    //Method = CredentialValidationMethodTypes.Implicit,
                    //Provider = new Uri("http://www.example.com/ImplicitAuth"),
                    UserId = Guid.NewGuid().ToString("N"),
                    Token = Guid.NewGuid().ToString("N"),
                }
            };
            var response = new BlackBarLabs.Api.Resources.Options()
            {
                Get = credentialProviders,
            };

            var responseMessage = request.CreateResponse(System.Net.HttpStatusCode.OK, response);
            return responseMessage.ToTask();
        }

        #endregion
    }
}

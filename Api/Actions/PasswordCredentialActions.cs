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

namespace EastFive.Security.SessionServer.Api
{
    public static class PasswordCredentialActions
    {
        public static async Task<HttpResponseMessage> CreateAsync(this Resources.PasswordCredential credential,
            HttpRequestMessage request, UrlHelper url)
        {
            return await request.Headers.Authorization.HasSiteAdminAuthorization(
                async () =>
                {
                    var response = await CreatePasswordCredentialAsync(credential, request, url);
                    return response;
                },
                (why) => request.CreateResponse(HttpStatusCode.Forbidden).AddReason(why).ToTask());
        }

        private static async Task<HttpResponseMessage> CreatePasswordCredentialAsync(Resources.PasswordCredential credential,
            HttpRequestMessage request, UrlHelper url)
        {
            var actorId = credential.Actor.ToGuid();
            var loginProviderTaskGetter = (Func<Task<IIdentityService>>)
                request.Properties[ServicePropertyDefinitions.IdentityService];
            var loginProviderTask = loginProviderTaskGetter();
            var loginProvider = await loginProviderTask;
            var callbackUrl = url.GetLocation<Controllers.OpenIdResponseController>();
            var loginUrl = loginProvider.GetLoginUrl(("http://orderowl.com"), 0, new byte[] { }, callbackUrl);

            var claims = new System.Security.Claims.Claim[] { };
            var context = request.GetSessionServerContext();
            var creationResults = await context.PasswordCredentials.CreatePasswordCredentialsAsync(
                credential.Id.UUID, actorId.Value,
                credential.UserId, credential.IsEmail, credential.Token, credential.ForceChange,
                credential.LastEmailSent, loginUrl,
                claims.ToArray(),
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
                () => request.CreateResponse(HttpStatusCode.ServiceUnavailable),
                (why) => request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason(why));
            return creationResults;
        }

        public static async Task<HttpResponseMessage> PutAsync(this Resources.PasswordCredential credential,
            HttpRequestMessage request, UrlHelper url)
        {

            var loginProviderTaskGetter = (Func<Task<IIdentityService>>)
                request.Properties[ServicePropertyDefinitions.IdentityService];
            var loginProviderTask = loginProviderTaskGetter();
            var loginProvider = await loginProviderTask;
            var callbackUrl = url.GetLocation<Controllers.OpenIdResponseController>();
            var loginUrl = loginProvider.GetLoginUrl(("http://orderowl.com"), 0, new byte[] { }, callbackUrl);

            var context = request.GetSessionServerContext();
            var claims = new System.Security.Claims.Claim[] { };
            var creationResults = await context.PasswordCredentials.UpdatePasswordCredentialAsync(credential.Id.UUID,
                credential.Token, credential.ForceChange, credential.LastEmailSent, loginUrl,
                claims,
                () => request.CreateResponse(HttpStatusCode.NoContent),
                () => request.CreateResponse(HttpStatusCode.NotFound),
                () => request.CreateResponse(HttpStatusCode.ServiceUnavailable),
                (why) => request.CreateResponse(HttpStatusCode.Conflict).AddReason($"Update failed:{why}"));
            return creationResults;
        }

        #region Actionables

        public static async Task<HttpResponseMessage> QueryAsync(this Resources.Queries.PasswordCredentialQuery credential,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return await credential.ParseAsync(request,
                q => QueryByIdAsync(q.Id.ParamSingle(), request, urlHelper),
                q => QueryByActorId(q.Actor.ParamSingle(), request, urlHelper));
        }

        private static async Task<HttpResponseMessage> QueryByIdAsync(Guid passwordCredentialId, HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.PasswordCredentials.GetPasswordCredentialAsync(passwordCredentialId,
                (passwordCredential) =>
                {
                    var response = request.CreateResponse(HttpStatusCode.OK,
                        Convert(passwordCredential, urlHelper));
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound),
                (why) => request.CreateResponse(HttpStatusCode.NotFound));
        }

        private async static Task<HttpResponseMessage[]> QueryByActorId(Guid actorId, HttpRequestMessage request, UrlHelper urlHelper)
        {
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

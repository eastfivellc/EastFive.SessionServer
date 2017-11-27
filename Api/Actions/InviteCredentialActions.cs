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
using EastFive.Security.SessionServer.Configuration;

namespace EastFive.Security.SessionServer.Api
{
    public static class InviteCredentialActions
    {
        #region Queries
        
        public static async Task<HttpResponseMessage> QueryAsync(this Resources.Queries.InviteCredentialQuery query,
            HttpRequestMessage request, UrlHelper urlHelper)
        {
            return await query.ParseAsync(request,
                q => QueryByIdAsync(q.Id.ParamSingle(), request, urlHelper),
                q => QueryByTokenAsync(q.Token.ParamSingle(), CredentialValidationMethodTypes.Voucher, request, urlHelper),
                q => QueryByActorAsync(q.Actor.ParamSingle(), request, urlHelper));
        }

        private static async Task<HttpResponseMessage> QueryByIdAsync(Guid inviteId, HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.Credentials.GetInviteAsync(inviteId,
                (invite) =>
                {
                    var response = request.CreateResponse(HttpStatusCode.OK, Convert(invite, urlHelper));
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound));
        }

        private static async Task<HttpResponseMessage> QueryByTokenAsync(Guid token, CredentialValidationMethodTypes method, HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.GetLoginProvider(method,
                async loginProvider =>
                {
                    return await await context.Credentials.GetInviteByTokenAsync(token,
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
                        async (actorId, extraParams) =>
                        {
                            return await await context.Sessions.CreateToken(actorId, Guid.NewGuid(),
                                (bearerToken, refreshToken) =>
                                {
                                    return Library.configurationManager.GetRedirectUriAsync(CredentialValidationMethodTypes.Token,
                                        actorId, bearerToken, refreshToken, extraParams, default(Uri),
                                        (redirectUrl) =>
                                        {
                                            var response = request.CreateResponse(HttpStatusCode.Redirect);
                                            response.Headers.Location = redirectUrl;
                                            return response;
                                        },
                                        (paramName, whyInvalid) => request.CreateResponse(HttpStatusCode.Conflict)
                                            .AddReason($"{paramName} is invalid:{whyInvalid}"),
                                        (why) => request.CreateResponse(HttpStatusCode.Conflict)
                                            .AddReason(why));
                                },
                                () => request.CreateResponse(HttpStatusCode.Conflict)
                                    .AddReason("Invite does not match an account in the system")
                                    .ToTask(),
                                (why) => request.CreateResponse(HttpStatusCode.Conflict).AddReason(why)
                                    .ToTask());
                        },
                        () => request.CreateResponse(HttpStatusCode.NotFound).AddReason("Already used").ToTask(),
                        () => request.CreateResponse(HttpStatusCode.NotFound).ToTask());
                },
                () => request.CreateResponse(HttpStatusCode.ServiceUnavailable)
                    .AddReason("Login service for provided method is not enabled")
                    .ToTask(),
                (why) => request.CreateResponse(HttpStatusCode.InternalServerError)
                    .AddReason(why)
                    .ToTask());
        }

        private static async Task<HttpResponseMessage[]> QueryByActorAsync(Guid actorId, HttpRequestMessage request, UrlHelper urlHelper)
        {
            var context = request.GetSessionServerContext();
            return await context.Credentials.GetInvitesByActorAsync(actorId,
                (invites) =>
                {
                    var responses = invites
                        .Select(invite =>
                            request.CreateResponse(HttpStatusCode.OK, Convert(invite, urlHelper)))
                        .ToArray();
                    return responses;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound).AsEnumerable().ToArray());
        }

        private static Resources.InviteCredential Convert(Invite invite, UrlHelper urlHelper)
        {
            return new Resources.InviteCredential
            {
                Id = urlHelper.GetWebId<Controllers.InviteCredentialController>(invite.id),
                Actor = Library.configurationManager.GetActorLink(invite.actorId, urlHelper),
                Email = invite.email,
                LastEmailSent = invite.lastSent,
            };
        }

        #endregion

        public static async Task<HttpResponseMessage> CreateAsync(this Resources.InviteCredential credential,
            HttpRequestMessage request, UrlHelper url)
        {
            var actorId = credential.Actor.ToGuid();
            if (!actorId.HasValue)
                return request.CreateResponse(HttpStatusCode.BadRequest).AddReason("Actor value must be set");

            return await request.GetActorIdClaimsAsync(ClaimsDefinitions.AccountIdClaimType,
                async (performingActorId, claims) =>
                {
                    var context = request.GetSessionServerContext();
                    var creationResults = await context.Credentials.SendEmailInviteAsync(
                        credential.Id.UUID, actorId.Value, credential.Email,
                        performingActorId, claims.ToArray(),
                        (inviteId, token) => url.GetLocation<Controllers.InviteCredentialController>().SetQueryParam("token", token.ToString("N")),
                        () => request.CreateResponse(HttpStatusCode.Created),
                        () => request.CreateResponse(HttpStatusCode.Conflict)
                            .AddReason($"Invite already exists"),
                        () => request.CreateResponse(HttpStatusCode.Conflict)
                            .AddReason($"Credential mapping not found"),
                        () => request.CreateResponse(HttpStatusCode.Unauthorized),
                        () => request.CreateResponse(HttpStatusCode.ServiceUnavailable),
                        (why) => request.CreateResponse(HttpStatusCode.Conflict)
                            .AddReason(why));
                    return creationResults;
                });
        }

        public static async Task<HttpResponseMessage> DeleteAsync(this Resources.Queries.InviteCredentialQuery query,
           HttpRequestMessage request, UrlHelper urlHelper)
        {
            return await request.GetActorIdClaimsAsync(ClaimsDefinitions.AccountIdClaimType,
                async (performingActorId, claims) => await query.ParseAsync(request,
                    q => DeleteByIdAsync(q.Id.ParamSingle(), request, urlHelper, performingActorId, claims)));
        }

        private static async Task<HttpResponseMessage> DeleteByIdAsync(Guid inviteId,
            HttpRequestMessage request, UrlHelper urlHelper,
            Guid performingActorId, System.Security.Claims.Claim [] claims)
        {
            var context = request.GetSessionServerContext();
            return await context.Credentials.DeleteByIdAsync(inviteId, performingActorId, claims,
                () =>
                {
                    var response = request.CreateResponse(HttpStatusCode.NoContent);
                    return response;
                },
                () => request.CreateResponse(HttpStatusCode.NotFound),
                () => request.CreateResponse(HttpStatusCode.Unauthorized));
        }
    }
}

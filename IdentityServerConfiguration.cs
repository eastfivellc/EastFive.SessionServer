﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Routing;
using BlackBarLabs.Api.Resources;
using BlackBarLabs;
using BlackBarLabs.Extensions;
using BlackBarLabs.Api;
using System.Security.Claims;

namespace EastFive.Security.SessionServer
{
    public class IdentityServerConfiguration<TActorController> : IConfigureIdentityServer
    {
        public const string parameterAuthorizationId = "authorizationId";
        public const string parameterToken = "token";
        public const string parameterRefreshToken = "refreshToken";

        public virtual Task<bool> CanAdministerCredentialAsync(Guid actorInQuestion, Guid actorTakingAction, System.Security.Claims.Claim[] claims)
        {
            if (actorInQuestion == actorTakingAction)
                return true.ToTask();

            if (IsSuperAdmin(actorTakingAction))
                return true.ToTask();

            return false.ToTask();
        }

        private bool IsSuperAdmin(Guid actorTakingAction)
        {
            return EastFive.Web.Configuration.Settings.GetGuid(
                EastFive.Api.Configuration.SecurityDefinitions.ActorIdSuperAdmin,
                (actorIdSuperAdmin) =>
                {
                    if (actorIdSuperAdmin == actorTakingAction)
                        return true;

                    return false;
                },
                (why) => false);
        }

        public virtual WebId GetActorLink(Guid actorId, UrlHelper urlHelper)
        {
            return urlHelper.GetWebId<TActorController>(actorId);
        }

        public virtual Task<TResult> GetRedirectUriAsync<TResult>(CredentialValidationMethodTypes validationType,
                Guid? authorizationId,
                string token, string refreshToken,
                IDictionary<string, string> authParams,
            Func<Uri, TResult> onSuccess,
            Func<string, string, TResult> onInvalidParameter,
            Func<string, TResult> onFailure)
        {
            if(authParams.ContainsKey(Configuration.AuthorizationParameters.RedirectUri))
            {
                Uri redirectUri;
                var redirectUriString = authParams[Configuration.AuthorizationParameters.RedirectUri];
                if (!Uri.TryCreate(redirectUriString, UriKind.Absolute, out redirectUri))
                    return onInvalidParameter("REDIRECT", $"BAD URL in redirect call:{redirectUriString}").ToTask();
                var redirectUrl = SetRedirectParameters(redirectUri, authorizationId, token, refreshToken);
                return onSuccess(redirectUrl).ToTask();
            }

            return EastFive.Web.Configuration.Settings.GetUri(
                EastFive.Security.SessionServer.Configuration.AppSettings.LandingPage,
                (redirectUri) =>
                {
                    var redirectUrl = SetRedirectParameters(redirectUri, authorizationId, token, refreshToken);
                    return onSuccess(redirectUrl);
                },
                (why) => onFailure(why)).ToTask();
        }

        protected Uri SetRedirectParameters(Uri redirectUri, Guid? authorizationId, string token, string refreshToken)
        {
            var redirectUrl = redirectUri
                .SetQueryParam("authoriationId", authorizationId.Value.ToString("N"))
                .SetQueryParam(parameterAuthorizationId, authorizationId.Value.ToString("N"))
                .SetQueryParam(parameterToken, token)
                .SetQueryParam(parameterRefreshToken, refreshToken);
            return redirectUrl;
        }

        public virtual Task<TResult> CanActAsUsersAsync<TResult>(Guid actorTakingAction, System.Security.Claims.Claim[] claims, Func<TResult> canActAsUsers, Func<TResult> deny)
        {
            if (IsSuperAdmin(actorTakingAction))
                return canActAsUsers().ToTask();

            return deny().ToTask();
        }
    }
}
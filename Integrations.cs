using System;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;

using BlackBarLabs.Extensions;
using BlackBarLabs.Api;
using BlackBarLabs.Linq.Async;
using System.Security.Claims;
using System.Security.Cryptography;
using BlackBarLabs;

namespace EastFive.Security.SessionServer
{
    public class Integrations
    {
        private Context context;
        private Persistence.DataContext dataContext;

        internal Integrations(Context context, Persistence.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }
        
        public async Task<TResult> CreateLinkAsync<TResult>(Guid authenticationRequestId, 
                Uri callbackLocation,
                CredentialValidationMethodTypes method, Uri redirectUrl,
                Guid authenticationId, Guid actorId, System.Security.Claims.Claim[] claims,
            Func<Session, TResult> onSuccess,
            Func<TResult> onAlreadyExists,
            Func<string, TResult> onUnauthorized,
            Func<TResult> onCredentialSystemNotAvailable,
            Func<string, TResult> onCredentialSystemNotInitialized,
            Func<string, TResult> onFailure)
        {
            if (!await Library.configurationManager.CanAdministerCredentialAsync(authenticationId, actorId, claims))
                return onUnauthorized($"Provided token does not permit access to link {authenticationId} to a login");
            return await context.GetLoginProvider(method,
                async (provider) =>
                {
                    var sessionId = SecureGuid.Generate();
                    var result = await this.dataContext.AuthenticationRequests.CreateAsync(authenticationRequestId,
                            method, AuthenticationActions.link, authenticationId, redirectUrl,
                        () => BlackBarLabs.Security.Tokens.JwtTools.CreateToken(sessionId, callbackLocation, TimeSpan.FromMinutes(30),
                            (token) => onSuccess(
                                new Session()
                                {
                                    id = authenticationRequestId,
                                    method = method,
                                    action = AuthenticationActions.signin,
                                    loginUrl = provider.GetLoginUrl(authenticationRequestId, callbackLocation),
                                    logoutUrl = provider.GetLogoutUrl(authenticationRequestId, callbackLocation),
                                    redirectUrl = redirectUrl,
                                    authorizationId = authenticationId,
                                    token = token,
                                }),
                            why => onFailure(why),
                            (param, why) => onFailure($"Invalid configuration for {param}:{why}")),
                        onAlreadyExists);
                    return result;
                },
                onCredentialSystemNotAvailable.AsAsyncFunc(),
                onCredentialSystemNotInitialized.AsAsyncFunc());
        }

        internal Task<TResult> DeleteByIdAsync<TResult>(Guid inviteId,
            Guid performingActorId, System.Security.Claims.Claim [] claims,
            Func<TResult> onSuccess, 
            Func<TResult> onNotFound,
            Func<TResult> onUnathorized)
        {
            return this.dataContext.CredentialMappings.DeleteInviteCredentialAsync(inviteId,
                async (current, deleteAsync) =>
                {
                    if (!await Library.configurationManager.CanAdministerCredentialAsync(
                        current.actorId, performingActorId, claims))
                        return onUnathorized();

                    await deleteAsync();
                    return onSuccess();
                },
                onNotFound);
        }
        
        private static Session Convert(Persistence.AuthenticationRequest authenticationRequestStorage)
        {
            return new Session
            {
                id = authenticationRequestStorage.id,
                method = authenticationRequestStorage.method,
                action = authenticationRequestStorage.action,
                token = authenticationRequestStorage.token,
                authorizationId = authenticationRequestStorage.authorizationId,
                extraParams = authenticationRequestStorage.extraParams,
                redirectUrl = authenticationRequestStorage.redirect,
            };
        }
    }
}

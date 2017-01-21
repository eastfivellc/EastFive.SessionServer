using System;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

using BlackBarLabs.Extensions;
using System.Security.Claims;

namespace EastFive.Security.SessionServer
{
    public class CredentialMappings
    {
        private Context context;
        private Persistence.Azure.DataContext dataContext;

        internal CredentialMappings(Context context, Persistence.Azure.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }

        internal Task<TResult> CreateAsync<TResult>(Guid credentialMappingId,
            Guid actorId, Guid? loginId,
            System.Security.Claims.Claim[] claims,
            Func<TResult> success,
            Func<TResult> onMappingAlreadyExists,
            Func<TResult> onLoginAlreadyUsed)
        {
            return this.dataContext.CredentialMappings.CreateCredentialMappingAsync(credentialMappingId,
                actorId, loginId,
                success,
                onMappingAlreadyExists,
                onLoginAlreadyUsed);
        }
        
        public async Task<TResult> CreatePasswordCredentialsAsync<TResult>(Guid credentialId, Guid credentialMappingId,
            string username, bool isEmail, string token, bool forceChange,
            System.Security.Claims.Claim[] claims,
            Func<TResult> onSuccess,
            Func<string, TResult> authenticationFailed,
            Func<TResult> credentialAlreadyExists,
            Func<TResult> credentialMappingNotFound,
            Func<TResult> serviceNotAvailable,
            Func<string, TResult> onFailure)
        {
            var loginProvider = await this.context.LoginProvider;

            var createLoginResult = await await loginProvider.CreateLoginAsync("User",
                username, isEmail, token, forceChange,
                async loginId =>
                {
                    Func<Func<TResult>, Task<TResult>> rollbackUserCreate =
                        async (onFail) =>
                        {
                            await loginProvider.DeleteLoginAsync(loginId);
                            return onFail();
                        };
                    var result = await await dataContext.CredentialMappings.UpdateLoginIdAsync(
                        credentialMappingId,
                        async (credentialMapping, updateAsync) =>
                        {
                            await updateAsync(loginId);
                            return onSuccess().ToTask();
                        },
                        () => rollbackUserCreate(credentialAlreadyExists),
                        () => rollbackUserCreate(credentialMappingNotFound));

                    return result;
                },
                (why) => authenticationFailed(why).ToTask());
            return createLoginResult;
        }
        
        public async Task<TResult> SendEmailInviteAsync<TResult>(Guid inviteId, Guid credentialMappingId, string email,
                System.Security.Claims.Claim[] claim,
                Func<Guid, Guid, Uri> getRedirectLink,
            Func<TResult> success,
            Func<TResult> inviteAlreadyExists,
            Func<TResult> onCredentialMappingDoesNotExists,
            Func<TResult> onServiceNotAvailable,
            Func<string, TResult> onFailed)
        {
            var token = BlackBarLabs.Security.SecureGuid.Generate();
            var result = await await this.dataContext.CredentialMappings.CreateInviteAsync(inviteId,
                credentialMappingId, email, token,
                async () =>
                {
                    var mailService = this.context.MailService;
                    var resultMail = await mailService.SendEmailMessageAsync(email, string.Empty,
                        "newaccounts@orderowl.com", "New Account Services",
                        "newaccount",
                        new Dictionary<string, string>()
                        {
                            { "subject", "New Order Owl Account" },
                            { "create_account_link", getRedirectLink(inviteId, token).AbsoluteUri }
                        },
                        null,
                        (sentCode) => success(),
                        () => onServiceNotAvailable(),
                        (why) => onFailed(why));
                    return resultMail;
                },
                () => inviteAlreadyExists().ToTask(),
                () => onCredentialMappingDoesNotExists().ToTask());
            return result;
        }

        internal Task<TResult> GetInviteAsync<TResult>(Guid inviteId,
            Func<byte [], TResult> success,
            Func<TResult> onAlreadyUsed,
            Func<TResult> notFound)
        {
            return this.dataContext.CredentialMappings.FindInviteAsync(inviteId,
                (actorId, loginId) =>
                {
                    if (loginId.HasValue)
                        return onAlreadyUsed();
                    var state = inviteId.ToByteArray();
                    return success(state);
                },
                () => notFound());
        }

        internal Task<TResult> GetInviteByTokenAsync<TResult>(Guid token,
            Func<byte[], TResult> redirect,
            Func<Guid, TResult> success,
            Func<TResult> onAlreadyUsed,
            Func<TResult> notFound)
        {
            return this.dataContext.CredentialMappings.FindInviteByTokenAsync(token,
                (inviteId, actorId, loginId) =>
                {
                    if (loginId.HasValue)
                        return success(actorId);

                    var state = token.ToByteArray();
                    return redirect(state);
                },
                () => notFound());
        }
    }
}

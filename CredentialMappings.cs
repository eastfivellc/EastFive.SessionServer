using System;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

using BlackBarLabs.Extensions;
using System.Security.Claims;

namespace EastFive.Security.SessionServer
{
    public struct CredentialMapping
    {
        public Guid id;
        public Guid actorId;
        public Guid? loginId;
    }

    public struct Invite
    {
        public Guid id;
        public Guid credentialMappingId;
        public string email;
    }

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

        internal Task<TResult> GetAsync<TResult>(Guid credentialMappingId,
            Func<Guid, Guid?, TResult> success,
            Func<TResult> notFound)
        {
            return this.dataContext.CredentialMappings.FindByIdAsync(credentialMappingId,
                (actorId, loginId) => success(actorId, loginId),
                () => notFound());
        }
        
        internal Task<TResult> GetByActorAsync<TResult>(Guid actorId,
            Func<CredentialMapping[], TResult> success,
            Func<TResult> notFound)
        {
            return this.dataContext.CredentialMappings.FindByActorAsync(actorId,
                (credentialMappings) => success(credentialMappings.Select(Convert).ToArray()),
                () => notFound());
        }

        private CredentialMapping Convert(Persistence.Azure.CredentialMapping mapping)
        {
            return new CredentialMapping
            {
                id = mapping.id,
                actorId = mapping.actorId,
                loginId = mapping.loginId,
            };
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
            Func<Invite, TResult> success,
            Func<TResult> notFound)
        {
            return this.dataContext.CredentialMappings.FindInviteAsync(inviteId,
                (credentialMappingId, email) =>
                {
                    return success(new Invite
                    {
                        id = inviteId,
                        credentialMappingId = credentialMappingId,
                        email = email,
                    });
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

        internal Task<TResult> GetInvitesByActorAsync<TResult>(Guid actorId,
            Func<Invite[], TResult> success,
            Func<TResult> notFound)
        {
            return this.dataContext.CredentialMappings.FindInviteByActorAsync(actorId,
                (invites) =>
                {
                    return success(invites);
                },
                () => notFound());
        }
    }
}

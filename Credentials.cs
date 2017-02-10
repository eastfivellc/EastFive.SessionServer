using System;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;

using BlackBarLabs.Extensions;
using System.Security.Claims;
using BlackBarLabs;
using BlackBarLabs.Linq.Async;

namespace EastFive.Security.SessionServer
{
    public struct Invite
    {
        public Guid id;
        public Guid actorId;
        public string email;
        internal bool isToken;
        internal DateTime? lastSent;
    }

    public class Credentials
    {
        private Context context;
        private Persistence.DataContext dataContext;

        internal Credentials(Context context, Persistence.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }
        
        #region InviteCredential

        public async Task<TResult> SendEmailInviteAsync<TResult>(Guid inviteId, Guid actorId, string email,
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
                actorId, email, token, DateTime.UtcNow, false,
                async () =>
                {
                    var templateName = ConfigurationManager.AppSettings[Configuration.EmailTemplateDefinitions.InviteNewAccount];
                    if (string.IsNullOrEmpty(templateName))
                        return onFailed($"Email template setting not found.  Expected template value for key {Configuration.EmailTemplateDefinitions.InviteNewAccount}");

                    var mailService = this.context.MailService;
                    var resultMail = await mailService.SendEmailMessageAsync(email, string.Empty,
                        "newaccounts@orderowl.com", "New Account Services",
                        templateName,
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
                () => inviteAlreadyExists().ToTask());
            return result;
        }

        internal Task<TResult> GetInviteAsync<TResult>(Guid inviteId,
            Func<Invite, TResult> success,
            Func<TResult> notFound)
        {
            return this.dataContext.CredentialMappings.FindInviteAsync(inviteId, false,
                (actorId, email, lastSent) =>
                {
                    return success(new Invite
                    {
                        id = inviteId,
                        actorId = actorId,
                        email = email,
                        lastSent = lastSent,
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
            return this.dataContext.CredentialMappings.FindInviteByActorAsync(actorId, false,
                (invites) =>
                {
                    return success(invites);
                },
                () => notFound());
        }

        #endregion
        
        public Task<TResult> LookupAccountIdAsync<TResult>(Guid loginId,
            Func<Guid, TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return this.dataContext.CredentialMappings.LookupCredentialMappingAsync(loginId, onSuccess, onNotFound)
        }

        #region Tokens

        public async Task<TResult> CreateTokenCredentialAsync<TResult>(Guid inviteId, Guid actorId, string email,
                System.Security.Claims.Claim[] claim,
                Func<Guid, Guid, Uri> getRedirectLink,
            Func<TResult> success,
            Func<TResult> inviteAlreadyExists,
            Func<TResult> onServiceNotAvailable,
            Func<string, TResult> onFailed)
        {
            var token = BlackBarLabs.Security.SecureGuid.Generate();
            var result = await await this.dataContext.CredentialMappings.CreateInviteAsync(inviteId,
                actorId, email, token, DateTime.UtcNow, true,
                async () =>
                {
                    var templateName = ConfigurationManager.AppSettings[Configuration.EmailTemplateDefinitions.LoginToken];
                    if (string.IsNullOrEmpty(templateName))
                        return onFailed($"Email template setting not found.  Expected template value for key {Configuration.EmailTemplateDefinitions.LoginToken}");

                    var mailService = this.context.MailService;
                    var resultMail = await mailService.SendEmailMessageAsync(email, string.Empty,
                        "newaccounts@orderowl.com", "New Account Services",
                        templateName,
                        new Dictionary<string, string>()
                        {
                            { "subject", "New Order Owl Account" },
                            { "token_login_link", getRedirectLink(inviteId, token).AbsoluteUri }
                        },
                        null,
                        (sentCode) => success(),
                        () => onServiceNotAvailable(),
                        (why) => onFailed(why));
                    return resultMail;
                },
                () => inviteAlreadyExists().ToTask());
            return result;
        }

        internal async Task<TResult> UpdateTokenCredentialAsync<TResult>(Guid tokenCredentialId,
                string email, DateTime? lastSent,
                System.Security.Claims.Claim[] claim,
                Func<Guid, Guid, Uri> getRedirectLink,
            Func<TResult> onSuccess,
            Func<TResult> onNoChange,
            Func<TResult> onNotFound,
            Func<TResult> onServiceNotAvailable,
            Func<string, TResult> onFailure)
        {
            var result = await this.dataContext.CredentialMappings.UpdateTokenCredentialAsync(tokenCredentialId,
                async (emailCurrent, lastSentCurrent, token, saveAsync) =>
                {
                    if (
                        (lastSent.HasValue && lastSentCurrent.HasValue && lastSent.Value > lastSentCurrent.Value) ||
                        (lastSent.HasValue && (!lastSentCurrent.HasValue)) ||
                        String.Compare(emailCurrent, email) != 0)
                    {
                        var templateName = ConfigurationManager.AppSettings[Configuration.EmailTemplateDefinitions.LoginToken];
                        if (string.IsNullOrEmpty(templateName))
                            return onFailure($"Email template setting not found.  Expected template value for key {Configuration.EmailTemplateDefinitions.LoginToken}");

                        if (String.IsNullOrWhiteSpace(email))
                            email = emailCurrent;
                        var mailService = this.context.MailService;
                        var resultMail = await await mailService.SendEmailMessageAsync(email, string.Empty,
                            "newaccounts@orderowl.com", "New Account Services",
                            templateName,
                            new Dictionary<string, string>()
                            {
                                { "subject", "New Order Owl Account" },
                                { "token_login_link", getRedirectLink(tokenCredentialId, token).AbsoluteUri }
                            },
                            null,
                            async (sentCode) =>
                            {
                                if (!lastSent.HasValue)
                                    lastSent = DateTime.UtcNow;
                                await saveAsync(email, lastSent);
                                return onSuccess();
                            },
                            () => onServiceNotAvailable().ToTask(),
                            (why) => onFailure(why).ToTask());
                        return resultMail;
                    }
                    return onNoChange();
                },
                () => onNotFound());
            return result;
        }

        internal Task<TResult> DeleteByIdAsync<TResult>(Guid inviteId,
            Func<TResult> onSuccess, 
            Func<TResult> onNotFound)
        {
            return this.dataContext.CredentialMappings.DeleteInviteCredentialAsync(inviteId,
                onSuccess, onNotFound);
        }

        internal Task<TResult> GetTokenCredentialAsync<TResult>(Guid inviteId,
            Func<Invite, TResult> success,
            Func<TResult> notFound)
        {
            return this.dataContext.CredentialMappings.FindInviteAsync(inviteId, true,
                (actorId, email, lastSent) =>
                {
                    return success(new Invite
                    {
                        id = inviteId,
                        actorId = actorId,
                        email = email,
                        lastSent = lastSent,
                        isToken = true,
                    });
                },
                () => notFound());
        }

        internal async Task<TResult> GetTokenCredentialByTokenAsync<TResult>(Guid token,
            Func<Guid, Guid, string, string, TResult> success,
            Func<TResult> notFound)
        {
            return await await this.dataContext.CredentialMappings.FindTokenCredentialByTokenAsync(token,
                async (inviteId, actorId) =>
                {
                    var sessionId = Guid.NewGuid();
                    var result = await this.context.Sessions.CreateAsync(sessionId, actorId,
                        new System.Security.Claims.Claim[] { },
                        (jwtToken, refreshToken) =>
                        {
                            return success(sessionId, actorId, jwtToken, refreshToken);
                        },
                        () => default(TResult)); // Should only happen if generated Guid is not unique ;-O
                    return result;
                },
                () => notFound().ToTask());
        }

        internal Task<TResult> GetTokenCredentialByActorAsync<TResult>(Guid actorId,
            Func<Invite[], TResult> success,
            Func<TResult> notFound)
        {
            return this.dataContext.CredentialMappings.FindInviteByActorAsync(actorId, true,
                (invites) =>
                {
                    return success(invites);
                },
                () => notFound());
        }

        internal Task<TResult> DeleteTokenByIdAsync<TResult>(Guid inviteId,
            Func<TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return this.dataContext.CredentialMappings.DeleteInviteCredentialAsync(inviteId,
                onSuccess, onNotFound);
        }

        #endregion

    }
}

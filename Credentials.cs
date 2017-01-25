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

    public struct PasswordCredential
    {
        public Guid id;
        public Guid actorId;
        public string userId;
        public bool isEmail;
        internal bool forceChangePassword;
    }

    public class Credentials
    {
        private Context context;
        private Persistence.Azure.DataContext dataContext;

        internal Credentials(Context context, Persistence.Azure.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }

        #region Password Credential

        public async Task<TResult> CreatePasswordCredentialsAsync<TResult>(Guid passwordCredentialId, Guid actorId,
            string username, bool isEmail, string token, bool forceChange,
            DateTime? emailLastSent, Uri loginUrl,
            System.Security.Claims.Claim[] claims,
            Func<TResult> onSuccess,
            Func<TResult> credentialAlreadyExists,
            Func<TResult> onRelationshipAlreadyExists,
            Func<TResult> onLoginAlreadyUsed,
            Func<TResult> onServiceNotAvailable,
            Func<string, TResult> onFailure)
        {
            var loginProvider = await this.context.LoginProvider;

            var createLoginResult = await await loginProvider.CreateLoginAsync("User",
                username, isEmail, token, forceChange,
                async loginId =>
                {
                    var result = await await dataContext.CredentialMappings.CreatePasswordCredentialAsync(
                        passwordCredentialId, actorId, loginId, emailLastSent,
                        async () =>
                        {
                            if (!isEmail || !emailLastSent.HasValue)
                                return onSuccess();
                            var resultMail = await SendInvitePasswordAsync(username, token, loginUrl,
                                onSuccess, onServiceNotAvailable, onFailure);

                            return resultMail;
                        },
                        async () =>
                        {
                            await loginProvider.DeleteLoginAsync(loginId);
                            return credentialAlreadyExists();
                        },
                        async () =>
                        {
                            await loginProvider.DeleteLoginAsync(loginId);
                            return onRelationshipAlreadyExists();
                        },
                        async () =>
                        {
                            await loginProvider.DeleteLoginAsync(loginId);
                            return onLoginAlreadyUsed();
                        });

                    return result;
                },
                (why) => onFailure(why).ToTask());
            return createLoginResult;
        }

        internal async Task<TResult> GetPasswordCredentialAsync<TResult>(Guid passwordCredentialId,
            Func<PasswordCredential, TResult> success,
            Func<TResult> notFound,
            Func<string, TResult> onServiceNotAvailable)
        {
            return await await this.dataContext.CredentialMappings.FindPasswordCredentialAsync(passwordCredentialId,
                async (actorId, loginId) =>
                {
                    var loginProvider = await this.context.LoginProvider;
                    return await loginProvider.GetLoginAsync(loginId,
                        (userId, isEmail, forceChangePassword) =>
                        {
                            return success(new PasswordCredential
                            {
                                id = passwordCredentialId,
                                actorId = actorId,
                                userId = userId,
                                isEmail = isEmail,
                                forceChangePassword = forceChangePassword,
                            });
                        },
                        () => notFound(), // TODO: Log this
                        (why) => onServiceNotAvailable(why));
                },
                () => notFound().ToTask());
        }

        internal async Task<TResult> GetPasswordCredentialByActorAsync<TResult>(Guid actorId,
            Func<PasswordCredential[], TResult> success,
            Func<TResult> notFound,
            Func<string, TResult> onServiceNotAvailable)
        {
            return await await this.dataContext.CredentialMappings.FindPasswordCredentialByActorAsync(actorId,
                async (credentials) =>
                {
                    var loginProvider = await this.context.LoginProvider;
                    var pwCreds = await credentials.Select(
                        async credential =>
                        {
                            return await loginProvider.GetLoginAsync(credential.loginId,
                                (userId, isEmail, forceChangePassword) =>
                                {
                                    return new PasswordCredential
                                    {
                                        id = credential.id,
                                        actorId = actorId,
                                        userId = userId,
                                        isEmail = isEmail,
                                        forceChangePassword = forceChangePassword,
                                    };
                                },
                                () => default(PasswordCredential?), // TODO: Log this
                                (why) => default(PasswordCredential?));
                        })
                        .WhenAllAsync()
                        .SelectWhereHasValueAsync()
                        .ToArrayAsync();
                    return success(pwCreds);
                },
                () => notFound().ToTask());
        }

        internal async Task<TResult> UpdatePasswordCredentialAsync<TResult>(Guid passwordCredentialId,
            string password, bool forceChange, DateTime? emailLastSent, Uri loginUrl,
            System.Security.Claims.Claim[] claims,
            Func<TResult> onSuccess,
            Func<TResult> onNotFound,
            Func<TResult> onRelationshipAlreadyExists,
            Func<TResult> onLoginAlreadyUsed,
            Func<TResult> onServiceNotAvailable,
            Func<string, TResult> onFailure)
        {
            var resultUpdatePassword = await await dataContext.CredentialMappings.UpdatePasswordCredentialAsync(passwordCredentialId,
                async (loginId, username, isEmail, emailLastSentCurrent, updateEmailLastSentAsync)  =>
                {
                    DiscriminatedDelegate<Guid, TResult, Task<TResult>> resultSuccess =
                        (success, fail) => success(loginId);
                    var failureMessage = "";
                    DiscriminatedDelegate<Guid, TResult, Task<TResult>> resultFailure =
                               (success, fail) => fail(onFailure(failureMessage));
                    if (emailLastSent.HasValue &&
                        (!emailLastSentCurrent.HasValue ||
                          emailLastSent.Value > emailLastSentCurrent.Value))
                    {
                        if (!isEmail)
                        {
                            failureMessage = "UserID is not an email address";
                            return resultFailure;
                        }
                        return await SendInvitePasswordAsync(username, "*********", loginUrl,
                            () => resultSuccess,
                            () =>
                            {
                                DiscriminatedDelegate<Guid, TResult, Task<TResult>> resultServiceUnavailable =
                                    (success, fail) => fail(onServiceNotAvailable());
                                return resultServiceUnavailable;
                            },
                            (why) =>
                            {
                                failureMessage = why;
                                return resultFailure;
                            });
                    }
                    
                    return resultSuccess;
                },
                () =>
                {
                    DiscriminatedDelegate<Guid, TResult, Task<TResult>> result =
                           (success, fail) => fail(onNotFound());
                    return result.ToTask();
                });
            return await resultUpdatePassword(
                async (loginId) =>
                {
                    if (string.IsNullOrWhiteSpace(password))
                        return onSuccess();

                    var loginProvider = await this.context.LoginProvider;
                    return loginProvider.UpdateLoginPassword(password,
                        () => onSuccess(),
                        (why) => onFailure(why));
                },
                (r) => r.ToTask());
        }

        private async Task<TResult> SendInvitePasswordAsync<TResult>(string emailAddress, string password, Uri loginUrl,
            Func<TResult> onSuccess,
            Func<TResult> onServiceNotAvailable,
            Func<string, TResult> onFailure)
        {
            var templateName = ConfigurationManager.AppSettings[Configuration.EmailTemplateDefinitions.InvitePassword];
            if (string.IsNullOrEmpty(templateName))
                return onFailure($"Email template setting not found.  Expected template value for key {Configuration.EmailTemplateDefinitions.InvitePassword}");
            
            var mailService = this.context.MailService;
            var resultMail = await mailService.SendEmailMessageAsync(emailAddress, string.Empty,
                "newaccounts@orderowl.com", "New Account Services",
                templateName,
                new Dictionary<string, string>()
                {
                                    { "subject",    "New Order Owl Account" },
                                    { "login_link", loginUrl.AbsoluteUri },
                                    { "username",   emailAddress },
                                    { "password",   password }
                },
                null,
                (sentCode) => onSuccess(),
                () => onServiceNotAvailable(),
                (why) => onFailure(why));
            return resultMail;
        }

        #endregion

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
                (actorId, email) =>
                {
                    return success(new Invite
                    {
                        id = inviteId,
                        actorId = actorId,
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
            return this.dataContext.CredentialMappings.FindInviteByActorAsync(actorId, false,
                (invites) =>
                {
                    return success(invites);
                },
                () => notFound());
        }

        #endregion

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

        internal Task<TResult> GetTokenCredentialAsync<TResult>(Guid inviteId,
            Func<Invite, TResult> success,
            Func<TResult> notFound)
        {
            return this.dataContext.CredentialMappings.FindInviteAsync(inviteId, true,
                (actorId, email) =>
                {
                    return success(new Invite
                    {
                        id = inviteId,
                        actorId = actorId,
                        email = email,
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

        #endregion

    }
}

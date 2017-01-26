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
    public struct PasswordCredential
    {
        public Guid id;
        public Guid actorId;
        public string userId;
        public bool isEmail;
        internal bool forceChangePassword;
        internal DateTime? lastSent;
    }

    public class PasswordCredentials
    {
        private Context context;
        private Persistence.DataContext dataContext;

        internal PasswordCredentials(Context context, Persistence.DataContext dataContext)
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
            Func<Guid, TResult> onUsernameAlreadyInUse,
            Func<TResult> onPasswordInsufficent,
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
                    var result = await await dataContext.PasswordCredentials.CreatePasswordCredentialAsync(
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
                (loginId) => onUsernameAlreadyInUse(loginId).ToTask(),
                () => onPasswordInsufficent().ToTask(),
                (why) => onFailure(why).ToTask());
            return createLoginResult;
        }

        internal async Task<TResult> GetPasswordCredentialAsync<TResult>(Guid passwordCredentialId,
            Func<PasswordCredential, TResult> success,
            Func<TResult> notFound,
            Func<string, TResult> onServiceNotAvailable)
        {
            return await await this.dataContext.PasswordCredentials.FindPasswordCredentialAsync(passwordCredentialId,
                async (actorId, loginId, lastSent) =>
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
                                lastSent = lastSent,
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
            return await await this.dataContext.PasswordCredentials.FindPasswordCredentialByActorAsync(actorId,
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
                                        lastSent = credential.lastSent,
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
            Func<TResult> onServiceNotAvailable,
            Func<string, TResult> onFailure)
        {
            var resultUpdatePassword = await dataContext.PasswordCredentials.UpdatePasswordCredentialAsync(passwordCredentialId,
                async (loginId, emailLastSentCurrent, updateEmailLastSentAsync)  =>
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
                        var loginProvider = await this.context.LoginProvider;
                        var resultGetLogin = await await loginProvider.GetLoginAsync(loginId,
                            async (username, isEmail, forceChangePassword) =>
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
                            },
                            () => resultFailure.ToTask(),
                            (why) => resultFailure.ToTask());
                        return resultGetLogin;
                    }
                    return resultSuccess;
                },
                () =>
                {
                    DiscriminatedDelegate<Guid, TResult, Task<TResult>> result =
                           (success, fail) => fail(onNotFound());
                    return result;
                });
            return await resultUpdatePassword(
                async (loginId) =>
                {
                    if (string.IsNullOrWhiteSpace(password))
                        return onSuccess();

                    var loginProvider = await this.context.LoginProvider;
                    return await loginProvider.UpdateLoginPasswordAsync(loginId, password, forceChange,
                        () => onSuccess(),
                        (why) => onFailure(why),
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
        
        internal async Task<TResult> DeletePasswordCredentialAsync<TResult>(Guid passwordCredentialId,
            Func<TResult> success,
            Func<TResult> notFound,
            Func<string, TResult> onServiceNotAvailable)
        {
            return await await this.dataContext.PasswordCredentials.DeletePasswordCredentialAsync(passwordCredentialId,
                async (loginId) =>
                {
                    var loginProvider = await this.context.LoginProvider;
                    await loginProvider.DeleteLoginAsync(loginId);
                    return success();
                },
                () => notFound().ToTask());
        }

        #endregion
        
    }
}

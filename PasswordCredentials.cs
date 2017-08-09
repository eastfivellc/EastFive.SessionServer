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
            Guid performingActorId, System.Security.Claims.Claim[] claims,
            Func<TResult> onSuccess,
            Func<TResult> credentialAlreadyExists,
            Func<Guid, TResult> onUsernameAlreadyInUse,
            Func<TResult> onPasswordInsufficent,
            Func<TResult> onRelationshipAlreadyExists,
            Func<TResult> onLoginAlreadyUsed,
            Func<TResult> onUnathorized,
            Func<TResult> onServiceNotAvailable,
            Func<string, TResult> onFailure)
        {
            //if (!await Library.configurationManager.CanAdministerCredentialAsync(
            //    actorId, performingActorId, claims))
            //    return onUnathorized();

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

        public async Task<TResult> CreatePasswordCredentialsAsync<TResult>(Guid passwordCredentialId, Guid actorId,
            string displayName, string username, bool isEmail, string token, bool forceChange,
            DateTime? emailLastSent, Uri loginUrl,
            Func<TResult> onSuccess,
            Func<TResult> credentialAlreadyExists,
            Func<
                Guid,
                Func<
                    Func<PasswordCredential, TResult>,
                    Func<TResult>,
                    Task<TResult>>,
                Task<TResult>> onUsernameAlreadyInUse,
            Func<TResult> onPasswordInsufficent,
            Func<TResult> onRelationshipAlreadyExists,
            Func<TResult> onLoginAlreadyUsed,
            Func<TResult> onServiceNotAvailable,
            Func<string, TResult> onFailure)
        {
            var loginProvider = await this.context.LoginProvider;

            var createLoginResult = await await loginProvider.CreateLoginAsync(displayName,
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
                (loginId) =>
                {
                    return onUsernameAlreadyInUse(loginId,
                        async (found, notInSystem) =>
                        {
                            return await dataContext.PasswordCredentials.FindPasswordCredentialByLoginIdAsync(loginId,
                                (match) =>
                                {
                                    return found(new PasswordCredential
                                    {
                                        id = match.id,
                                        actorId = match.actorId,
                                        userId = username,
                                        isEmail = isEmail,
                                        forceChangePassword = forceChange,
                                        lastSent = emailLastSent,
                                    });
                                },
                                () => notInSystem());
                        });
                },
                () => onPasswordInsufficent().ToTask(),
                (why) => onFailure(why).ToTask());
            return createLoginResult;
        }

        internal async Task<TResult> GetPasswordCredentialAsync<TResult>(Guid passwordCredentialId,
                Guid actorPerformingId, System.Security.Claims.Claim[] claims,
            Func<PasswordCredential, TResult> success,
            Func<TResult> notFound,
            Func<TResult> onUnauthorized,
            Func<string, TResult> onServiceNotAvailable)
        {
            return await await this.dataContext.PasswordCredentials.FindPasswordCredentialAsync(passwordCredentialId,
                async (actorId, loginId, lastSent) =>
                {
                    if (!await Library.configurationManager.CanAdministerCredentialAsync(actorId, actorPerformingId, claims))
                        return onUnauthorized();

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

        public struct LoginInfo
        {
            public string UserId;
            public Guid LoginId;
            public Guid ActorId;
        }

        internal async Task<TResult> GetAllLoginInfoAsync<TResult>(
            Func<LoginInfo[], TResult> success)
        {
            var finalResult = await await this.dataContext.PasswordCredentials.FindAllAsync<Task<TResult>>(
                async (passwordCredentialInfos) =>
                {
                    var loginProvider = await context.LoginProvider;
                    var result = await passwordCredentialInfos.Select(
                        async passwordCredentialInfo =>
                        {
                            try
                            {
                                var credInfo = await await loginProvider.GetLoginAsync(passwordCredentialInfo.LoginId,
                                    (userId, isEmail, forceChangePassword) =>
                                    {
                                        return this.context.Credentials.LookupAccountIdAsync(passwordCredentialInfo.LoginId,
                                            (actorId) => new LoginInfo
                                            {
                                                LoginId = passwordCredentialInfo.LoginId,
                                                UserId = userId,
                                                ActorId = actorId,
                                            },
                                            () => default(LoginInfo?));
                                    },
                                    () => { return default(LoginInfo?).ToTask(); }, 
                                    (why) => { return default(LoginInfo?).ToTask(); });
                                    return credInfo;
                            }
                            catch (Exception ex)
                            {
                                return default(LoginInfo?); 
                            }
                        }).WhenAllAsync()
                        .SelectWhereHasValueAsync()
                        .ToArrayAsync();
                    return success(result);
                });
            return finalResult;
        }

        internal async Task<TResult> UpdatePasswordCredentialAsync<TResult>(Guid passwordCredentialId,
            string password, bool forceChange, DateTime? emailLastSent, Uri loginUrl,
            Guid performingActorId, System.Security.Claims.Claim[] claims,
            Func<TResult> onSuccess,
            Func<TResult> onNotFound,
            Func<TResult> onUnathorized,
            Func<TResult> onServiceNotAvailable,
            Func<string, TResult> onFailure)
        {
            var resultUpdatePassword = await dataContext.PasswordCredentials.UpdatePasswordCredentialAsync(passwordCredentialId,
                async (actorId, loginId, emailLastSentCurrent, updateEmailLastSentAsync)  =>
                {
                    DiscriminatedDelegate<Guid, TResult, Task<TResult>> resultSuccess =
                        (success, fail) => success(loginId);
                    var failureMessage = "";
                    DiscriminatedDelegate<Guid, TResult, Task<TResult>> resultFailure =
                        (success, fail) => fail(onFailure(failureMessage));

                    if (!await Library.configurationManager.CanAdministerCredentialAsync(
                        actorId, performingActorId, claims))
                        return (success, fail) => fail(onUnathorized());

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
                                return await await SendInvitePasswordAsync(username, "*********", loginUrl,
                                    async () =>
                                    {
                                        await updateEmailLastSentAsync(emailLastSent.Value);
                                        return resultSuccess;
                                    },
                                    () =>
                                    {
                                        DiscriminatedDelegate<Guid, TResult, Task<TResult>> resultServiceUnavailable =
                                        (success, fail) => fail(onServiceNotAvailable());
                                        return resultServiceUnavailable.ToTask();
                                    },
                                    (why) =>
                                    {
                                        failureMessage = why;
                                        return resultFailure.ToTask();
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
            var resultMail = await mailService.SendEmailMessageAsync(
                templateName, 
                emailAddress, string.Empty,
                "newaccounts@orderowl.com", "New Account Services",
                "New Order Owl Account",
                new Dictionary<string, string>()
                {
                    { "login_link", loginUrl.AbsoluteUri },
                    { "username",   emailAddress },
                    { "password",   password }
                },
                default(IDictionary<string, IDictionary<string, string>[]>),
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

using System;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;

using BlackBarLabs.Extensions;
using System.Security.Claims;
using BlackBarLabs;
using BlackBarLabs.Linq;
using BlackBarLabs.Linq.Async;

namespace EastFive.Security.SessionServer
{
    public struct PasswordCredential
    {
        public Guid id;
        public Guid actorId;
        public string displayName;
        public string userId;
        public bool isEmail;
        internal bool forceChangePassword;
        internal DateTime? lastSent;
    }

    public class PasswordCredentials
    {
        private Context context;
        private Persistence.DataContext dataContext;
        private IProvideLoginManagement managmentProvider;
        private IProvideLogin loginProvider;

        internal PasswordCredentials(Context context, Persistence.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
            // TODO: Refactor this class so it works with any Login Provider
            context.GetLoginProvider(CredentialValidationMethodTypes.Password,
                (identityService) =>
                {
                    this.loginProvider = identityService;
                    return true;
                },
                () => false,
                (why) => false);
            context.GetManagementProvider(CredentialValidationMethodTypes.Password,
                (identityService) =>
                {
                    this.managmentProvider = identityService;
                    return true;
                },
                () => false,
                (why) => false);
        }

        #region Password Credential

        public async Task<TResult> CreatePasswordCredentialsAsync<TResult>(Guid passwordCredentialId, Guid actorId,
            string displayName, string username, bool isEmail, string token, bool forceChange,
            DateTime? emailLastSent, Uri callbackUrl,
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
            if (!await Library.configurationManager.CanAdministerCredentialAsync(
                actorId, performingActorId, claims))
                return onUnathorized();
            
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = "User";

            var createLoginResult = await await managmentProvider.CreateAuthorizationAsync(displayName,
                username, isEmail, token, forceChange,
                async loginId =>
                {
                    var result = await await dataContext.PasswordCredentials.CreatePasswordCredentialAsync(
                        passwordCredentialId, actorId, loginId, emailLastSent,
                        async () =>
                        {
                            if (!isEmail || !emailLastSent.HasValue)
                                return onSuccess();

                            return await Web.Configuration.Settings.GetUri(Configuration.AppSettings.LandingPage,
                                async (landingPage) =>
                                {
                                    return await await context.AuthenticationRequests.CreateLoginAsync(Guid.NewGuid(), callbackUrl,
                                        CredentialValidationMethodTypes.Password, landingPage,
                                        async (authenticationRequest) =>
                                        {
                                            var loginUrl = authenticationRequest.loginUrl;
                                            var resultMail = await SendInvitePasswordAsync(username, token, loginUrl,
                                                onSuccess, onServiceNotAvailable, onFailure);
                                            return resultMail;
                                        },
                                        "GUID not unique".AsFunctionException<Task<TResult>>(),
                                        "Password system said not available while in use".AsFunctionException<Task<TResult>>(),
                                        "Password system said not initialized while in use".AsFunctionException<string, Task<TResult>>(),
                                        onFailure.AsAsyncFunc());
                                },
                                onFailure.AsAsyncFunc());
                        },
                        async () =>
                            await managmentProvider.DeleteAuthorizationAsync(loginId,
                                () => credentialAlreadyExists(),
                                (why) => onFailure(why),
                                () => onFailure("Service became unsupported after credentialAlreadyExists"),
                                (why) => onFailure(why)),
                        async () =>
                            await managmentProvider.DeleteAuthorizationAsync(loginId,
                                () => onRelationshipAlreadyExists(),
                                (why) => onFailure(why),
                                () => onFailure("Service became unsupported after onRelationshipAlreadyExists"),
                                (why) => onFailure(why)),
                        async () =>
                            await managmentProvider.DeleteAuthorizationAsync(loginId,
                                () => onRelationshipAlreadyExists(),
                                (why) => onFailure(why),
                                () => onFailure("Service became unsupported after onLoginAlreadyUsed"),
                                (why) => onFailure(why)));
                    return result;
                },
                (loginId) => onUsernameAlreadyInUse(loginId).ToTask(),
                () => onPasswordInsufficent().ToTask(),
                (why) => onFailure(why).ToTask(),
                () => onFailure("Service not supported").ToTask(),
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
                    
                    return await this.managmentProvider.GetAuthorizationAsync(loginId,
                        (loginInfo) =>
                        {
                            return success(new PasswordCredential
                            {
                                id = passwordCredentialId,
                                displayName = loginInfo.displayName,
                                actorId = actorId,
                                userId = loginInfo.userName,
                                isEmail = loginInfo.isEmail,
                                forceChangePassword = loginInfo.forceChangePassword,
                                lastSent = lastSent,
                            });
                        },
                        () => notFound(), // TODO: Log this
                        (why) => onServiceNotAvailable(why),
                        () => onServiceNotAvailable("not supported"),
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
                    var pwCreds = await credentials.Select(
                        async credential =>
                        {
                            return await managmentProvider.GetAuthorizationAsync(credential.loginId,
                                (loginInfo) =>
                                {
                                    return new PasswordCredential
                                    {
                                        id = credential.id,
                                        displayName = loginInfo.displayName,
                                        actorId = actorId,
                                        userId = loginInfo.userName,
                                        isEmail = loginInfo.isEmail,
                                        forceChangePassword = loginInfo.forceChangePassword,
                                        lastSent = credential.lastSent,
                                    };
                                },
                                () => default(PasswordCredential?), // TODO: Log this
                                (why) => default(PasswordCredential?),
                                () => default(PasswordCredential?),
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
            public LoginInfo(string userId, Guid loginId, Guid actorId, bool accountEnabled)
            {
                UserId = userId;
                LoginId = loginId;
                ActorId = actorId;
                AccountEnabled = accountEnabled;
            }

            public string UserId;
            public Guid LoginId;
            public Guid ActorId;
            public bool AccountEnabled;
        }
       
        internal async Task<TResult> GetAllLoginInfoAsync<TResult>(
            Func<LoginInfo[], TResult> success)
        {
            var finalResult = await await this.dataContext.PasswordCredentials.FindAllAsync(
                async (passwordCredentialInfos) =>
                {
                    return await await managmentProvider.GetAllAuthorizationsAsync(
                        async loginInfos => 
                        {
                            return await this.context.Credentials.GetAllAccountIdAsync(
                                map => // loginId, actorId
                                {
                                    return passwordCredentialInfos
                                        .Select(
                                            p =>
                                            {
                                                var actorId = map.Where(m => m.Item1 == p.LoginId).Select(m => m.Item2).FirstOrDefault();
                                                var loginInfo = loginInfos.FirstOrDefault(t => t.loginId == p.LoginId);
                                                var userName = loginInfo.userName;
                                                var accountEnabled = loginInfo.accountEnabled;
                                                return (default(Guid) == actorId || String.IsNullOrEmpty(userName)) 
                                                ? default(LoginInfo?) : new LoginInfo(userName, p.LoginId, actorId, accountEnabled);
                                            })
                                        .Where(x => x.HasValue)
                                        .Select(x => x.Value)
                                        .ToArray();
                                });
                        },
                        (why) => (new LoginInfo[] {}).ToTask(),
                        () => (new LoginInfo[] { }).ToTask(),
                        (why) => (new LoginInfo[] { }).ToTask());
                });
            return success(finalResult);
        }

        internal async Task<TResult> UpdatePasswordCredentialAsync<TResult>(Guid passwordCredentialId,
            string password, bool forceChange, DateTime? emailLastSent, Uri callbackUrl,
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
                        var resultGetLogin = await await managmentProvider.GetAuthorizationAsync(loginId,
                            async (loginInfo) =>
                            {
                                if (!loginInfo.isEmail)
                                {
                                    failureMessage = "UserID is not an email address";
                                    return resultFailure;
                                }

                                return await Web.Configuration.Settings.GetUri(Configuration.AppSettings.LandingPage,
                                    async (landingPage) =>
                                    {
                                        return await await context.AuthenticationRequests.CreateLoginAsync(Guid.NewGuid(), callbackUrl,
                                            CredentialValidationMethodTypes.Password, landingPage,
                                            async (authenticationRequest) =>
                                            {
                                                var loginUrl = authenticationRequest.loginUrl;
                                                // TODO: the purpose of the next line is to send the password. 
                                                // If we don't want it sent, don't update the last sent value!!!
                                                return await await SendInvitePasswordAsync(loginInfo.userName, "*********", loginUrl,
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
                                            "GUID not unique".AsFunctionException<Task<DiscriminatedDelegate<Guid, TResult, Task<TResult>>>>(),
                                            "Password system said not available while in use".AsFunctionException<Task<DiscriminatedDelegate<Guid, TResult, Task<TResult>>>>(),
                                            "Password system said not initialized while in use".AsFunctionException<string, Task<DiscriminatedDelegate<Guid, TResult, Task<TResult>>>>(),
                                            (why) =>
                                            {
                                                DiscriminatedDelegate<Guid, TResult, Task<TResult>> resultFailureCreateLogin =
                                                        (success, fail) => fail(onFailure(why));
                                                return resultFailureCreateLogin.ToTask();
                                            });
                                    },
                                    (why) =>
                                    {
                                        DiscriminatedDelegate<Guid, TResult, Task<TResult>> resultFailureConfig =
                                                (success, fail) => fail(onFailure(why));
                                        return resultFailureConfig.ToTask();
                                    });
                            },
                            () => resultFailure.ToTask(),
                            (why) => resultFailure.ToTask(),
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
                    
                    return await managmentProvider.UpdateAuthorizationAsync(loginId, password, forceChange,
                        () => onSuccess(),
                        (why) => onFailure(why),
                        () => onFailure("service unavailable"),
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
            
            var mailService = Web.Services.ServiceConfiguration.SendMessageService();
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
                    return await managmentProvider.DeleteAuthorizationAsync(loginId,
                        success,
                        onServiceNotAvailable,
                        () => onServiceNotAvailable("Not supported"), 
                        onServiceNotAvailable);
                },
                () => notFound().ToTask());
        }

        #endregion
        
    }
}

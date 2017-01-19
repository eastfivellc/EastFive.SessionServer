using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using BlackBarLabs.Extensions;
using System.Collections.Generic;

namespace EastFive.Security.SessionServer
{
    public struct AuthorizationCredential
    {
        public string userId;
        public string secret;
        public bool isEmail;
    }

    public struct Claim
    {
        public Guid claimId;
        public Uri issuer;
        public Uri type;
        public string value;
    }

    public class Authorizations
    {
        private Context context;
        private Persistence.Azure.DataContext dataContext;

        internal Authorizations(Context context, Persistence.Azure.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }
        
        #region Credentials
        
        
        public async Task<TResult> CreateCredentialsAsync<TResult>(Guid actorId,
            CredentialValidationMethodTypes method,
            string username, bool isEmail, string token, bool forceChange,
            System.Security.Claims.Claim [] claims,
            Func<Guid, Uri> getRedirectLink,
            Func<TResult> success,
            Func<string, TResult> authenticationFailed,
            Func<TResult> alreadyAssociated,
            Func<TResult> serviceNotAvailable,
            Func<string, TResult> onFail)
        {
            if (method == CredentialValidationMethodTypes.AzureADB2C)
            {
                if(String.IsNullOrWhiteSpace(token) && isEmail)
                    return await SendEmailInviteAsync(actorId, username,
                        getRedirectLink,
                        success, 
                        alreadyAssociated,
                        serviceNotAvailable,
                        onFail);

                return await CreateAccountAsync(actorId,
                    username, isEmail, token, forceChange,
                    success, authenticationFailed, alreadyAssociated);
            }
            throw new NotImplementedException();
        }

        private async Task<TResult> SendEmailInviteAsync<TResult>(Guid actorId, string email,
                Func<Guid, Uri> getRedirectLink,
            Func<TResult> success,
            Func<TResult> alreadyAssociated,
            Func<TResult> serviceNotAvailable,
            Func<string, TResult> onFailed)
        {
            var redirectId = BlackBarLabs.Security.SecureGuid.Generate();
            var result = await await this.dataContext.Authorizations.CreateCredentialRedirectAsync(redirectId,
                actorId, email,
                async () =>
                {
                    var mailService = this.context.MailService;
                    var resultMail = await mailService.SendEmailMessageAsync(email, string.Empty,
                        "newaccounts@orderowl.com", "New Account Services",
                        "newaccount",
                        new Dictionary<string, string>()
                        {
                            { "subject", "New Order Owl Account" },
                            { "create_account_link", getRedirectLink(redirectId).AbsoluteUri }
                        },
                        null,
                        (sentCode) => success(),
                        () => serviceNotAvailable(),
                        (why) => onFailed(why));
                    return resultMail;
                },
                () => alreadyAssociated().ToTask());
            return result;
        }
        
        internal Task<TResult> GetCredentialRedirectAsync<TResult>(Guid redirectId,
            Func<byte [], TResult> success,
            Func<TResult> onAlreadyUsed,
            Func<TResult> notFound)
        {
            return this.dataContext.Authorizations.FindCredentialRedirectAsync(redirectId,
                (used) =>
                {
                    if (used)
                        return onAlreadyUsed();
                    var state = redirectId.ToByteArray();
                    return success(state);
                },
                () => notFound());
        }

        private async Task<TResult> CreateAccountAsync<TResult>(Guid actorId,
            string username, bool isEmail, string token, bool forceChange,
            Func<TResult> success,
            Func<string, TResult> authenticationFailed,
            Func<TResult> alreadyAssociated)
        {
            var loginProvider = await this.context.LoginProvider;
            var result = await await loginProvider.CreateLoginAsync("User", username, isEmail, token, forceChange,
                async (loginId) =>
                {
                    return await this.dataContext.Authorizations.CreateCredentialAsync(loginId, actorId,
                        () => success(),
                        () => alreadyAssociated());
                },
                (why) => authenticationFailed(why).ToTask());
            return result;
        }

        public async Task<TResult> GetCredentialsAsync<TResult>(Guid authorizationId,
            Func<Guid, TResult> success,
            Func<TResult> authorizationDoesNotExists)
        {
            throw new NotImplementedException();
            //var provider = this.context.GetCredentialProvider(method);
            //return
            //    await
            //        provider.GetCredentialsAsync<TResult>(providerId, username,
            //            s => success(Guid.Parse(s)),
            //            authorizationDoesNotExists);
        }
        
        public async Task<TResult> UpdateCredentialsAsync<TResult>(Guid authorizationId,
            string username, bool isEmail, string token, bool forceChange,
            Func<TResult> success, 
            Func<TResult> authorizationDoesNotExists,
            Func<string, TResult> updateFailed)
        {
            throw new NotImplementedException();
            ////Updates the Credential Password
            //var provider = this.context.GetCredentialProvider(method);
            //var result = await await provider.UpdateTokenAsync(providerId, username, token,
            //    (returnToken) => Task.FromResult(success()),
            //    async () =>
            //    {
            //        if (method != CredentialValidationMethodTypes.Implicit)
            //            return authorizationDoesNotExists();
            //        return await CreateCredentialsAsync(authorizationId, 
            //            method, providerId, username, token,
            //            success,
            //            updateFailed,
            //            authorizationDoesNotExists,
            //            (authIdExists) => updateFailed("already exists"));
            //    },
            //    () => Task.FromResult(updateFailed("failure")));
            //return result;
        }


        #endregion

    }
}

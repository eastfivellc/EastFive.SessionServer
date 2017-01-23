using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using BlackBarLabs.Extensions;
using System.Collections.Generic;
using BlackBarLabs;

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
        
        
        //public async Task<TResult> CreateCredentialsAsync<TResult>(Guid actorId,
        //    CredentialValidationMethodTypes method,
        //    string username, bool isEmail, string token, bool forceChange,
        //    System.Security.Claims.Claim [] claims,
        //    Func<TResult> success,
        //    Func<string, TResult> authenticationFailed,
        //    Func<TResult> alreadyAssociated,
        //    Func<TResult> serviceNotAvailable,
        //    Func<string, TResult> onFail)
        //{
        //    if (String.IsNullOrWhiteSpace(token) && isEmail)
        //        return await SendEmailInviteAsync(actorId, username,
        //            getRedirectLink,
        //            success,
        //            alreadyAssociated,
        //            serviceNotAvailable,
        //            onFail);

        //    return await CreateAccountAsync(actorId,
        //        username, isEmail, token, forceChange,
        //        success, authenticationFailed, alreadyAssociated);

        //    if (CredentialValidationMethodTypes.Voucher == method)
        //    {
        //        return SendTokenInviteAsync(actorId, username,
        //            getRedirectLink,
        //            success,
        //            onFail);
        //    }

        //    throw new NotImplementedException();
        //}

        //private async Task<TResult> SendTokenInviteAsync<TResult>(Guid inviteId, Guid actorId, string email,
        //    Func<Guid, Guid, Uri> getRedirectLink,
        //    Func<TResult> success,
        //    Func<string, TResult> onFailed,
        //    Func<TResult> serviceNotAvailable)
        //{
        //    var token = BlackBarLabs.Security.SecureGuid.Generate();
        //    var result = await await this.dataContext.CredentialMappings.CreateTokenAsync(inviteId,
        //        actorId, email, token,
        //        async () =>
        //        {
        //                    var mailService = this.context.MailService;
        //                    var resultMail = await mailService.SendEmailMessageAsync(email, string.Empty,
        //                        "newaccounts@orderowl.com", "New Account Services",
        //                        "newaccount",
        //                        new Dictionary<string, string>()
        //                        {
        //                            { "subject", "Login to OrderOwl" },
        //                            { "create_account_link", getRedirectLink(inviteId, token).AbsoluteUri }
        //                        },
        //                        null,
        //                        (sentCode) => success(),
        //                        () => serviceNotAvailable(),
        //                        (why) => onFailed(why));
        //                    return resultMail;
        //        },
        //        () => onFailed("Already associated").ToTask());
        //    return result;
        //}

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

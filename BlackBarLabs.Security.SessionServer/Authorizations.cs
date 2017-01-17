using System;
using System.Linq;
using System.Threading.Tasks;

using BlackBarLabs.Extensions;

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
            string username, bool isEmail, string token, bool forceChange,
            System.Security.Claims.Claim [] claims,
            Func<TResult> success,
            Func<string, TResult> authenticationFailed,
            Func<TResult> authorizationDoesNotExists,
            Func<Guid, TResult> alreadyAssociated)
        {
            // ... validates the provider credentials before accepting / storing them.
            //var result = await this.context.LoginProvider.CreateLoginAsync(String.Empty, username, isEmail, token, forceChange,
            //    async (loginId) =>
            //    {
            //        return await this.dataContext.Authorizations.CreateCredentialProviderAsync(loginId,
            //                actorId,
            //            () => success(),
            //            () => authorizationDoesNotExists(),
            //            (alreadyAssociatedAuthorizationId) => alreadyAssociated(alreadyAssociatedAuthorizationId));
            //    },
            //    (why) => authenticationFailed(why).ToTask());
            //return result;
            throw new NotImplementedException();
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

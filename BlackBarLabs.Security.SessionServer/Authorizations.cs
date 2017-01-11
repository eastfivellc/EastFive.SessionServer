using System;
using System.Linq;
using System.Threading.Tasks;

using BlackBarLabs.Extensions;
using BlackBarLabs.Security.AuthorizationServer;
using BlackBarLabs.Security.Session;

namespace BlackBarLabs.Security.SessionServer
{
    public struct AuthorizationCredential
    {
        public string userId;
        public string secret;
        public bool isEmail;
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
        
        public async Task<TResult> CreateAsync<TResult>(string displayName,
                string username, bool isEmail, string secret, bool forceChange,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyExists,
            Func<string, TResult> onFail)
        {
            var client = new AzureADB2C.B2CGraphClient();
            var user = new AzureADB2C.Resources.User()
            {
                AccountEnabled = true,
                DisplayName = displayName,
                SignInNames = new[] {
                    new AzureADB2C.Resources.User.SignInName
                    {
                        Type = isEmail? "emailAddress" : "userName",
                        Value = username,
                    }
                },
                PasswordProfile = new AzureADB2C.Resources.User.PasswordProfileResource
                {
                    ForceChangePasswordNextLogin = forceChange,
                    Password = secret,
                },
            };
            var result = await await client.CreateUser(user,
                async (authorizationId) =>
                {
                    var resultData = await this.dataContext.Authorizations.CreateAuthorizationAsync(authorizationId,
                            null,
                        () => onSuccess(),
                        () => onAlreadyExists());
                    return resultData;
                },
                (why) => onFail(why).ToTask());
            
            return result;
        }
        
        public async Task<TResult> CreateCredentialsAsync<TResult>(Guid authorizationId, 
            CredentialValidationMethodTypes method, Uri providerId, string username, string token,
            Func<TResult> success,
            Func<string, TResult> authenticationFailed,
            Func<TResult> authorizationDoesNotExists,
            Func<Guid, TResult> alreadyAssociated)
        {
            // ... validates the provider credentials before accepting / storing them.
            var provider = this.context.GetCredentialProvider(method);
            var result = await await provider.RedeemTokenAsync(providerId, username, token,
                async (authId, claims) =>
                {
                    return await this.dataContext.Authorizations.CreateCredentialProviderAsync(authorizationId,
                        providerId, username,
                        () => success(),
                        () => authorizationDoesNotExists(),
                        (alreadyAssociatedAuthorizationId) => alreadyAssociated(alreadyAssociatedAuthorizationId));
                },
                (errorMessage) => authenticationFailed(errorMessage).ToTask(),
                () => Task.FromResult(default(TResult)));
            return result;
        }

        public async Task<TResult> GetCredentialsAsync<TResult>(CredentialValidationMethodTypes method, Uri providerId, string username,
            Func<Guid, TResult> success,
            Func<TResult> authorizationDoesNotExists)
        {
            var provider = this.context.GetCredentialProvider(method);
            return
                await
                    provider.GetCredentialsAsync<TResult>(providerId, username,
                        s => success(Guid.Parse(s)),
                        authorizationDoesNotExists);
        }
        
        public async Task<TResult> UpdateCredentialsAsync<TResult>(Guid authorizationId,
            CredentialValidationMethodTypes method, Uri providerId, string username, string token,
            Func<TResult> success, 
            Func<TResult> authorizationDoesNotExists,
            Func<string, TResult> updateFailed)
        {
            //Updates the Credential Password
            var provider = this.context.GetCredentialProvider(method);
            var result = await await provider.UpdateTokenAsync(providerId, username, token,
                (returnToken) => Task.FromResult(success()),
                async () =>
                {
                    if (method != CredentialValidationMethodTypes.Implicit)
                        return authorizationDoesNotExists();
                    return await CreateCredentialsAsync(authorizationId, 
                        method, providerId, username, token,
                        success,
                        updateFailed,
                        authorizationDoesNotExists,
                        (authIdExists) => updateFailed("already exists"));
                },
                () => Task.FromResult(updateFailed("failure")));
            return result;
        }


        #endregion

    }
}

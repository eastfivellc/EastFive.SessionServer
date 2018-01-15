using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using BlackBarLabs.Extensions;
using System.Security.Claims;
using EastFive.Security.CredentialProvider.ImplicitCreation;
using System.Collections.Generic;

namespace EastFive.Security.SessionServer.CredentialProvider.ImplicitCreation
{
    public class ImplicitlyCreatedCredentialProvider : IProvideLoginManagement, IProvideAuthorization
    {
        public static Task<TResult> InitializeAsync<TResult>(
            Func<IProvideLogin, TResult> onProvideLogin,
            Func<IProvideAuthorization, TResult> onProvideAuthorization,
            Func<TResult> onProvideNothing,
            Func<string, TResult> onFailure)
        {
            return onProvideNothing().ToTask();
        }

        public CredentialValidationMethodTypes Method => CredentialValidationMethodTypes.Implicit;

        public Type CallbackController => typeof(Api.Controllers.TokenController);

        public Task<TResult> RedeemTokenAsync<TResult>(IDictionary<string, string> extraParams,
            Func<string, Guid?, Guid?, IDictionary<string, string>, TResult> onSuccess,
            Func<Guid?, IDictionary<string, string>, TResult> onUnauthenticated,
            Func<string, TResult> onInvalidCredentials,
            Func<string, TResult> onCouldNotConnect,
            Func<string, TResult> onUnspecifiedConfiguration,
            Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }

        #region IProvideLoginManagement

        public Task<TResult> CreateAuthorizationAsync<TResult>(string displayName, string userId, bool isEmail, string secret, bool forceChange, Func<Guid, TResult> onSuccess, Func<Guid, TResult> usernameAlreadyInUse, Func<TResult> onPasswordInsufficent, Func<string, TResult> onServiceNotAvailable, Func<TResult> onServiceNotSupported, Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> GetAllAuthorizationsAsync<TResult>(Func<LoginInfo[], TResult> onFound, Func<string, TResult> onServiceNotAvailable, Func<TResult> onServiceNotSupported, Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> GetAuthorizationAsync<TResult>(Guid loginId,
            Func<LoginInfo, TResult> onSuccess,
            Func<TResult> onNotFound,
            Func<string, TResult> onServiceNotAvailable,
            Func<TResult> onServiceNotSupported,
            Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> UpdateAuthorizationAsync<TResult>(Guid loginId, string password, bool forceChange, Func<TResult> onSuccess, Func<string, TResult> onServiceNotAvailable, Func<TResult> onServiceNotSupported, Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> DeleteAuthorizationAsync<TResult>(Guid loginId, Func<TResult> onSuccess, Func<string, TResult> onServiceNotAvailable, Func<TResult> onServiceNotSupported, Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }

        #endregion
        
        public async Task<TResult> LookupAndDeleteUser<TResult>(string username, string token,
            Func<Guid, TResult> success,
            Func<TResult> invalidCredentials,
            Func<TResult> onNotFound, 
            Func<string, TResult> couldNotConnect)
        {
            // create hashed version of the password
            var tokenHashBytes = SHA512.Create().ComputeHash(Encoding.UTF8.GetBytes(token));
            var tokenHash = Convert.ToBase64String(tokenHashBytes);
            
            #region User MD5 hash to create a unique key for each providerId and username combination

            var providerId = ConfigurationManager.AppSettings.Get("BlackBarLabs.Security.CredentialProvider.ImplicitCreation.ProviderId");

            var concatination = providerId + username;
            var md5 = MD5.Create();
            byte[] md5data = md5.ComputeHash(Encoding.UTF8.GetBytes(concatination));
            var authId = new Guid(md5data);

            #endregion

            // Create or fetch the document with that key

            const string connectionStringKeyName = Configuration.AppSettings.Storage;
            var context = new BlackBarLabs.Persistence.Azure.DataStores(connectionStringKeyName);
            var result = await context.AzureStorageRepository.DeleteIfAsync<CredentialsDocument, TResult>(authId,
                async (document, delete) =>
                {
                    // If there currently is a credential document for this providerId / username combination
                    // then check the stored password hash with the provided password hash and respond accordingly. 
                    if (String.Compare(document.AccessToken, tokenHash, false) == 0)
                    {
                        var r = success(authId);
                        await delete();
                        return r;
                    }

                    return invalidCredentials();
                },
                () => onNotFound());
            return result;
        }
        

    }
}
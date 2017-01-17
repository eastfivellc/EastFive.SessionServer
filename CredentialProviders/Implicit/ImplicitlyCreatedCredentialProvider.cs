using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using BlackBarLabs.Extensions;
using System.Security.Claims;

namespace EastFive.Security.CredentialProvider.ImplicitCreation
{
    public class ImplicitlyCreatedCredentialProvider
    {
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

            const string connectionStringKeyName = "Azure.Authorization.Storage";
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

        //public async Task<TResult> GetCredentialsAsync<TResult>(Uri providerId, string username,
        //    Func<string, TResult> success, Func<TResult> doesNotExist)
        //{
        //    #region User MD5 hash to create a unique key for each providerId and username combination

        //    var concatination = providerId.AbsoluteUri + username;
        //    var md5 = MD5.Create();
        //    byte[] md5data = md5.ComputeHash(Encoding.UTF8.GetBytes(concatination));
        //    var md5guid = new Guid(md5data);

        //    #endregion

        //    // Fetch the document with that key

        //    const string connectionStringKeyName = "Azure.Authorization.Storage";
        //    var context = new BlackBarLabs.Persistence.Azure.DataStores(connectionStringKeyName);

        //    var result = await context.AzureStorageRepository.FindByIdAsync<CredentialsDocument, TResult>(md5guid, 
        //        document => success(document.RowKey), 
        //        doesNotExist);

        //    return result;
        //}
    }
}
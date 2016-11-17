using System;
using System.Threading.Tasks;

namespace BlackBarLabs.Security.CredentialProvider.OpenIdConnect
{
    public class OpenIdConnectCredentialProvider : IProvideCredentials
    {
        public Task<TResult> RedeemTokenAsync<TResult>(Uri providerId, string username, string accessToken, 
            Func<string, TResult> success, Func<string, TResult> invalidCredentials, Func<TResult> couldNotConnect)
        {
            var returnValue = String.Compare(username, accessToken) == 0 ? 
                success(accessToken) : invalidCredentials("Username and access token do not match");
            return Task.FromResult(returnValue);
        }

        public Task<TResult> UpdateTokenAsync<TResult>(Uri providerId, string username, string token, Func<string, TResult> success, Func<TResult> doesNotExist,
            Func<TResult> updateFailed)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> GetCredentialsAsync<TResult>(Uri providerId, string username, Func<string, TResult> success, Func<TResult> doesNotExist)
        {
            throw new NotImplementedException();
        }
    }
}

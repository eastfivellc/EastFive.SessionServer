using System;
using System.Threading.Tasks;
using Facebook;

namespace BlackBarLabs.Security.CredentialProvider.Facebook
{
    public class FacebookCredentialProvider : IProvideCredentials
    {
        public async Task<TResult> RedeemTokenAsync<TResult>(Uri providerId, string username, string accessToken,
            Func<string, TResult> success, Func<string, TResult> invalidCredentials, Func<TResult> couldNotConnect)
        {
            if (String.IsNullOrWhiteSpace(accessToken))
                return invalidCredentials("accessToken is null");
            var client = new FacebookClient(accessToken);
            try
            {
                dynamic result = await client.GetTaskAsync("me", new { fields = "name,id" });
                if (null == result)
                    return invalidCredentials("Cannot get token from Facebook");
                if (username != result.id)
                    return invalidCredentials("username and result.Id from Facebook do not match");
                return success(client.AccessToken);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("OAuthException"))
                    return invalidCredentials("OAuthException occurred");
                throw ex;
            }
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

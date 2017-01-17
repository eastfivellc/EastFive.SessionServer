using System;
using System.Threading.Tasks;
using Facebook;
using System.Security.Claims;

namespace EastFive.Security.CredentialProvider.Facebook
{
    public class FacebookCredentialProvider : IProvideCredentials
    {
        public async Task<TResult> RedeemTokenAsync<TResult>(string accessToken, 
            Func<Guid, Claim[], TResult> success,
            Func<string, TResult> invalidCredentials,
            Func<TResult> onAuthIdNotFound,
            Func<string, TResult> couldNotConnect)
        {
            if (String.IsNullOrWhiteSpace(accessToken))
                return invalidCredentials("accessToken is null");
            var client = new FacebookClient(accessToken);
            try
            {
                dynamic result = await client.GetTaskAsync("me", new { fields = "name,id" });
                if (null == result)
                    return invalidCredentials("Cannot get token from Facebook");
                var username = ""; // TODO: Lookup from database
                if (username != result.id)
                    return invalidCredentials("username and result.Id from Facebook do not match");
                throw new NotImplementedException();
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("OAuthException"))
                    return invalidCredentials("OAuthException occurred");
                throw ex;
            }
        }
    }
}

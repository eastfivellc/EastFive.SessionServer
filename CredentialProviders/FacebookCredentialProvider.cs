using System;
using System.Threading.Tasks;
using Facebook;
using System.Security.Claims;
using System.Collections.Generic;

namespace EastFive.Security.CredentialProvider.Facebook
{
    public class FacebookCredentialProvider : IProvideCredentials
    {
        public async Task<TResult> RedeemTokenAsync<TResult>(string token, Dictionary<string, string> extraParams,
            Func<Guid, IDictionary<string, string>, TResult> onSuccess,
            Func<string, TResult> onInvalidCredentials,
            Func<TResult> onAuthIdNotFound,
            Func<string, TResult> onCouldNotConnect,
            Func<string, TResult> onUnspecifiedConfiguration,
            Func<string, TResult> onFailure)
        {
            if (String.IsNullOrWhiteSpace(token))
                return onInvalidCredentials("accessToken is null");
            var client = new FacebookClient(token);
            try
            {
                dynamic result = await client.GetTaskAsync("me", new { fields = "name,id" });
                if (null == result)
                    return onInvalidCredentials("Cannot get token from Facebook");
                var username = ""; // TODO: Lookup from database
                if (username != result.id)
                    return onInvalidCredentials("username and result.Id from Facebook do not match");
                throw new NotImplementedException();
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("OAuthException"))
                    return onInvalidCredentials("OAuthException occurred");
                throw ex;
            }
        }
    }
}

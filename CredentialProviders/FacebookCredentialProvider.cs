using System;
using System.Threading.Tasks;
using Facebook;
using System.Security.Claims;
using System.Collections.Generic;
using EastFive.Security.SessionServer;
using BlackBarLabs.Extensions;

namespace EastFive.Security.CredentialProvider.Facebook
{
    public class FacebookCredentialProvider : IProvideLogin
    {
        public static Task<TResult> InitializeAsync<TResult>(
            Func<IProvideLogin, TResult> onProvideLogin,
            Func<IProvideAuthorization, TResult> onProvideAuthorization,
            Func<TResult> onProvideNothing,
            Func<string, TResult> onFailure)
        {
            return onProvideNothing().ToTask();
        }

        public CredentialValidationMethodTypes Method => CredentialValidationMethodTypes.Facebook;

        public Type CallbackController => typeof(SessionServer.Api.Controllers.ResponseController);

        public async Task<TResult> RedeemTokenAsync<TResult>(IDictionary<string, string> extraParams,
            Func<string, Guid?, Guid?, IDictionary<string, string>, TResult> onSuccess,
            Func<string, TResult> onInvalidCredentials,
            Func<string, TResult> onCouldNotConnect,
            Func<string, TResult> onUnspecifiedConfiguration,
            Func<string, TResult> onFailure)
        {
            var token = extraParams["fb_response"]; // TODO: Lookup real value
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

        #region IProvideLogin
        
        public Uri GetLoginUrl(Guid state, Uri responseControllerLocation)
        {
            throw new NotImplementedException();
        }

        public Uri GetSignupUrl(Guid state, Uri responseControllerLocation)
        {
            throw new NotImplementedException();
        }

        public Uri GetLogoutUrl(Guid state, Uri responseControllerLocation)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}

using BlackBarLabs.Extensions;
using EastFive.Security.SessionServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Security.CredentialProvider
{
    public class OAuthProvider : IProvideLogin
    {
        private readonly string clientId;
        private readonly string clientSecret;

        public OAuthProvider(string clientKey, string clientSecret)
        {
            this.clientId = clientKey;
            this.clientSecret = clientSecret;
        }
        
        public static TResult LoadFromConfig<TResult>(
            Func<OAuthProvider, TResult> onLoaded,
            Func<string, TResult> onConfigurationNotAvailable)
        {
            return Web.Configuration.Settings.GetString(SessionServer.Configuration.AppSettings.OAuth.Lightspeed.ClientKey,
                (clientKey) =>
                {
                    return Web.Configuration.Settings.GetString(SessionServer.Configuration.AppSettings.OAuth.Lightspeed.ClientSecret,
                        (clientSecret) =>
                        {
                            var provider = new OAuthProvider(clientKey, clientSecret);
                            return onLoaded(provider);
                        },
                        onConfigurationNotAvailable);
                },
                onConfigurationNotAvailable);
        }

        public static async Task<TResult> InitializeAsync<TResult>(
            Func<IProvideAuthorization, TResult> onProvideAuthorization,
            Func<TResult> onProvideNothing,
            Func<string, TResult> onFailure)
        {
            return await LoadFromConfig(
                (provider) => onProvideAuthorization(provider),
                (why) => onFailure(why)).ToTask();
        }

        public CredentialValidationMethodTypes Method => CredentialValidationMethodTypes.OAuth;

        public Task<TResult> RedeemTokenAsync<TResult>(IDictionary<string, string> extraParams, 
            Func<string, Guid?, Guid?, IDictionary<string, string>, TResult> onSuccess,
            Func<string, TResult> onInvalidCredentials,
            Func<string, TResult> onCouldNotConnect, 
            Func<string, TResult> onUnspecifiedConfiguration, 
            Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> CreateAuthorizationAsync<TResult>(string displayName, string userId, bool isEmail, string secret, bool forceChange, Func<Guid, TResult> onSuccess, Func<Guid, TResult> usernameAlreadyInUse, Func<TResult> onPasswordInsufficent, Func<string, TResult> onFail)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAuthorizationAsync(Guid loginId)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> GetAllAuthorizationsAsync<TResult>(Func<LoginInfo[], TResult> onFound, Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> GetAuthorizationAsync<TResult>(Guid loginId, Func<string, string, bool, bool, bool, TResult> onSuccess, Func<TResult> onNotFound, Func<string, TResult> onServiceNotAvailable)
        {
            throw new NotImplementedException();
        }

        public Uri GetLoginUrl(Guid state, Uri responseControllerLocation)
        {
            // response_type -- Ask for ‘code’ which will give you a temporary token that you can then use to get an access token.
            // https://cloud.merchantos.com/oauth/authorize.php?response_type=code&client_id={client_id}&scope={scope}&state={state}
            var loginScopes = "employee:register_read+employee:inventory+employee:admin_inventory";
            var stateString = state.ToString("N");
            var url = $"https://cloud.merchantos.com/oauth/authorize.php?response_type=code&client_id={this.clientId}&scope={loginScopes}&state={stateString}";
            return new Uri(url);
        }

        public Uri GetLoginUrl(string redirect_uri, byte mode, byte[] state, Uri responseControllerLocation)
        {
            throw new NotImplementedException();
        }

        public Uri GetLogoutUrl(string redirect_uri, byte mode, byte[] state, Uri responseControllerLocation)
        {
            throw new NotImplementedException();
        }

        public Uri GetSignupUrl(string redirect_uri, byte mode, byte[] state, Uri responseControllerLocation)
        {
            throw new NotImplementedException();
        }

        public TResult ParseState<TResult>(string state, Func<byte, byte[], IDictionary<string, string>, TResult> onSuccess, Func<string, TResult> invalidState)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> UpdateAuthorizationAsync<TResult>(Guid loginId, string password, bool forceChange, Func<TResult> onSuccess, Func<string, TResult> onServiceNotAvailable, Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }
    }
}

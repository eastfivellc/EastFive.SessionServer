using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EastFive.Security.SessionServer
{
    public interface IProvideAuthorization
    {
        CredentialValidationMethodTypes Method { get; }
        
        Type CallbackController { get; }

        /// <summary>
        /// This method validates that the provided token is valid for
        /// the specified username and provider.
        /// </summary>
        /// <param name="providerId">Identifies the provider to use</param>
        /// <param name="username">Identifiers the user in the remote system</param>
        /// <param name="token">Token/password to use to authenticate the user</param>
        /// <param name="success"></param>
        /// <param name="invalidCredentials"></param>
        /// <param name="couldNotConnect"></param>
        /// <returns>Value which will be stored for future access to this system. The return value must
        /// not be a default or empty string if the token was valid.</returns>
        Task<TResult> RedeemTokenAsync<TResult>(IDictionary<string, string> responseParams,
            Func<string, Guid?, Guid?, IDictionary<string, string>, TResult> onSuccess,
            Func<Guid?, IDictionary<string, string>, TResult> onNotAuthenticated,
            Func<string, TResult> onInvalidToken,
            Func<string, TResult> onCouldNotConnect,
            Func<string, TResult> onUnspecifiedConfiguration,
            Func<string, TResult> onFailure);
    }
}

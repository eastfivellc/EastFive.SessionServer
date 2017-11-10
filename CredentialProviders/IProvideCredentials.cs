using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EastFive.Security.CredentialProvider
{
    public interface IProvideCredentials
    {
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
        Task<TResult> RedeemTokenAsync<TResult>(string token,
            Func<Guid, IDictionary<string, string>, TResult> success,
            Func<string, TResult> invalidCredentials,
            Func<TResult> onAuthIdNotFound,
            Func<string, TResult> couldNotConnect,
            Func<string, TResult> unspecifiedConfiguration);
    }
}

using System;
using System.Threading.Tasks;

namespace BlackBarLabs.Security.CredentialProvider.Voucher
{
    public class VoucherCredentialProvider : IProvideCredentials
    {
        public async Task<TResult> RedeemTokenAsync<TResult>(Uri providerId, string username, string accessToken,
            Func<string, TResult> success, Func<string, TResult> invalidCredentials, Func<TResult> couldNotConnect)
        {
            var trustedProvider = Utilities.GetTrustedProviderId();
            var trimChars = new char[] { '/' };
            if (String.Compare(providerId.AbsoluteUri.TrimEnd(trimChars), trustedProvider.AbsoluteUri.TrimEnd(trimChars)) != 0)
                return invalidCredentials("ProviderId given does not match trustred ProviderId");

            var userNameId = await Task.FromResult(Guid.Parse(username));

            return Utilities.ValidateToken(accessToken,
                (authId) =>
                {
                    if (authId.CompareTo(userNameId) != 0)
                        return invalidCredentials(
                            String.Format("authId:[{0}] does not match userNameId:[{1}]", authId, userNameId));
                    return success(authId.ToString());
                },
                (errorMessage) => invalidCredentials(errorMessage),
                (errorMessage) => invalidCredentials(errorMessage),
                (errorMessage) => invalidCredentials(errorMessage));
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

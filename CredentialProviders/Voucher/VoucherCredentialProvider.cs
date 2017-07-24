using BlackBarLabs.Extensions;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EastFive.Security.CredentialProvider.Voucher
{
    public class VoucherCredentialProvider : IProvideCredentials
    {
        public Task<TResult> RedeemTokenAsync<TResult>(string accessToken,
            Func<Guid, IDictionary<string, string>, TResult> success,
            Func<string, TResult> invalidCredentials, Func<TResult> onAuthIdNotFound, Func<string, TResult> couldNotConnect)
        {
            //var trustedProvider = Utilities.GetTrustedProviderId();
            //var trimChars = new char[] { '/' };
            //if (String.Compare(providerId.AbsoluteUri.TrimEnd(trimChars), trustedProvider.AbsoluteUri.TrimEnd(trimChars)) != 0)
            //    return invalidCredentials("ProviderId given does not match trustred ProviderId");
            
            return Utilities.ValidateToken(accessToken,
                (authId) =>
                {
                    return success(authId, null);
                },
                (errorMessage) => invalidCredentials(errorMessage),
                (errorMessage) => invalidCredentials(errorMessage),
                (errorMessage) => invalidCredentials(errorMessage)).ToTask();
        }
    }
}

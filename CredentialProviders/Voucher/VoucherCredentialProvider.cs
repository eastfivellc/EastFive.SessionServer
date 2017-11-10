using BlackBarLabs.Extensions;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EastFive.Security.CredentialProvider.Voucher
{
    public class VoucherCredentialProvider : IProvideCredentials
    {
        public Task<TResult> RedeemTokenAsync<TResult>(string token, Dictionary<string, string> extraParams,
            Func<Guid, IDictionary<string, string>, TResult> onSuccess,
            Func<string, TResult> onInvalidCredentials,
            Func<TResult> onAuthIdNotFound,
            Func<string, TResult> onCouldNotConnect,
            Func<string, TResult> onUnspecifiedConfiguration,
            Func<string, TResult> onFailure)
        {
            //var trustedProvider = Utilities.GetTrustedProviderId();
            //var trimChars = new char[] { '/' };
            //if (String.Compare(providerId.AbsoluteUri.TrimEnd(trimChars), trustedProvider.AbsoluteUri.TrimEnd(trimChars)) != 0)
            //    return invalidCredentials("ProviderId given does not match trustred ProviderId");
            
            return Utilities.ValidateToken(token,
                (authId) =>
                {
                    return onSuccess(authId, null);
                },
                (errorMessage) => onInvalidCredentials(errorMessage),
                (errorMessage) => onInvalidCredentials(errorMessage),
                (errorMessage) => onInvalidCredentials(errorMessage),
                onUnspecifiedConfiguration).ToTask();
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

using EastFive.Security.LoginProvider;
using EastFive.Api.Services;
using BlackBarLabs.Extensions;

namespace EastFive.Security.CredentialProvider.AzureADB2C
{
    public class AzureADB2CProvider : IProvideCredentials
    {
        IIdentityService loginProvider;
        SessionServer.Context context;

        public AzureADB2CProvider(IIdentityService loginProvider, SessionServer.Context context)
        {
            this.loginProvider = loginProvider;
            this.context = context;
        }

        public async Task<TResult> RedeemTokenAsync<TResult>(string token, 
            Func<Guid, IDictionary<string, string>, TResult> success,
            Func<string, TResult> invalidCredentials,
            Func<TResult> onAuthIdNotFound, 
            Func<string, TResult> couldNotConnect)
        {
            return await loginProvider.ValidateToken(token,
                (claims) =>
                {
                    var claimType = Web.Configuration.Settings.Get(
                        EastFive.Security.SessionServer.Configuration.AppSettings.LoginIdClaimType);
                    var authClaims = claims.Claims
                        .Where(claim => claim.Type.CompareTo(claimType) == 0)
                        .ToArray();
                    if (authClaims.Length == 0)
                        return invalidCredentials($"Token does not contain claim for [{claimType}] which is necessary to operate with this system");
                    Guid authId;
                    if(!Guid.TryParse(authClaims[0].Value, out authId))
                        return invalidCredentials("User has invalid auth claim for this system");

                    // TODO: Load extra params from token claims
                    return success(authId, new Dictionary<string, string>());
                },
                invalidCredentials);
        }
    }
}

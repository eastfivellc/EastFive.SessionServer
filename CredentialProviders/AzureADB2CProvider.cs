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
        internal const string StateKey = "State";
        IIdentityService loginProvider;
        SessionServer.Context context;

        public AzureADB2CProvider(IIdentityService loginProvider, SessionServer.Context context)
        {
            this.loginProvider = loginProvider;
            this.context = context;
        }

        public async Task<TResult> RedeemTokenAsync<TResult>(string token, Dictionary<string, string> extraParams,
            Func<Guid, IDictionary<string, string>, TResult> onSuccess,
            Func<string, TResult> onInvalidCredentials,
            Func<TResult> onAuthIdNotFound,
            Func<string, TResult> onCouldNotConnect,
            Func<string, TResult> onUnspecifiedConfiguration,
            Func<string, TResult> onFailure)
        {
            return await loginProvider.ValidateToken(token,
                (claims) =>
                {
                    return Web.Configuration.Settings.GetString(
                            EastFive.Security.SessionServer.Configuration.AppSettings.LoginIdClaimType,
                        (claimType) =>
                        {
                            return loginProvider.ParseState(extraParams[StateKey],
                                (action, data, extraParamsFromState) =>
                                {
                                    var authClaims = claims.Claims
                                                 .Where(claim => claim.Type.CompareTo(claimType) == 0)
                                                 .ToArray();
                                    if (authClaims.Length == 0)
                                        return onFailure($"Token does not contain claim for [{claimType}] which is necessary to operate with this system");
                                    Guid authId;
                                    if (!Guid.TryParse(authClaims[0].Value, out authId))
                                        return onAuthIdNotFound(); //  ("User has invalid auth claim for this system");

                                    return onSuccess(authId, extraParamsFromState);
                                },
                                (why) => onFailure(why));
                        },
                        onUnspecifiedConfiguration);
                },
                onInvalidCredentials);
        }
    }
}

using System;
using System.Linq;
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

        public Task<TResult> RedeemTokenAsync<TResult>(string token, 
            Func<Guid, Claim[], TResult> success,
            Func<string, TResult> invalidCredentials,
            Func<TResult> onAuthIdNotFound, 
            Func<string, TResult> couldNotConnect)
        {
            return RedeemTokenInternalAsync(token, success,
                invalidCredentials,
                couldNotConnect);
        }
        
        public async Task<TResult> RedeemTokenInternalAsync<TResult>(string id_token,
            Func<Guid, System.Security.Claims.Claim[], TResult> success,
            Func<string, TResult> invalidToken,
            Func<string, TResult> couldNotConnect)
        {
            //TODO - Validate the token with AAD B2C here
            //Surely there is a library for this.  Actually, the OWIN library does this.  Look in OO API's Startup.Auth.cs.
            //If that cannot be leveraged, to get the public key:
            //https://login.microsoftonline.com/humagelorderowladb2cdev.onmicrosoft.com/v2.0/.well-known/openid-configuration?p=b2c_1_sign_up_sign_in
            //from there, follow the jwks_uri to here:
            //https://login.microsoftonline.com/humagelorderowladb2cdev.onmicrosoft.com/discovery/v2.0/keys?p=b2c_1_sign_up_sign_in
            //This will return the rolling keys to validate the jwt signature
            //We will want to cache the key here and only go fetch again if the signature look up fails.  The keys rotate about every 24 hours.

            return await loginProvider.ValidateToken(id_token,
                (claims) =>
                {
                    var claimType = Microsoft.Azure.CloudConfigurationManager.GetSetting(
                        EastFive.IdentityServer.Configuration.AADB2CDefinitions.LoginIdClaimType);
                    var authClaims = claims.Claims
                        .Where(claim => claim.Type.CompareTo(claimType) == 0)
                        .ToArray();
                    if (authClaims.Length == 0)
                        return invalidToken($"Token does not contain claim for [{claimType}] which is necessary to operate with this sytem");
                    Guid authId;
                    if(!Guid.TryParse(authClaims[0].Value, out authId))
                        return invalidToken("User has invalid auth claim for this system");

                    return success(authId, null);
                },
                invalidToken);
        }
    }
}

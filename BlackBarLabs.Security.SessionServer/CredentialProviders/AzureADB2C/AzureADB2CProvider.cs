using BlackBarLabs.Security.CredentialProvider;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BlackBarLabs.Security.CredentialProvider.AzureADB2C
{
    public class AzureADB2CProvider : BlackBarLabs.Security.CredentialProvider.IProvideCredentials
    {
        public Task<TResult> RedeemTokenAsync<TResult>(Uri providerId, string username, string id_token,
            Func<Guid, System.Security.Claims.Claim[], TResult> success,
            Func<string, TResult> invalidToken,
            Func<TResult> couldNotConnect)
        {
            return RedeemTokenInternalAsync(id_token, success, invalidToken, couldNotConnect);
        }

        public static async Task<TResult> RedeemTokenInternalAsync<TResult>(string id_token,
            Func<Guid, System.Security.Claims.Claim[], TResult> success,
            Func<string, TResult> invalidToken,
            Func<TResult> couldNotConnect)
        {
            //TODO - Validate the token with AAD B2C here
            //Surely there is a library for this.  Actually, the OWIN library does this.  Look in OO API's Startup.Auth.cs.
            //If that cannot be leveraged, to get the public key:
            //https://login.microsoftonline.com/humagelorderowladb2cdev.onmicrosoft.com/v2.0/.well-known/openid-configuration?p=b2c_1_sign_up_sign_in
            //from there, follow the jwks_uri to here:
            //https://login.microsoftonline.com/humagelorderowladb2cdev.onmicrosoft.com/discovery/v2.0/keys?p=b2c_1_sign_up_sign_in
            //This will return the rolling keys to validate the jwt signature
            //We will want to cache the key here and only go fetch again if the signature look up fails.  The keys rotate about every 24 hours.

            return await SessionServer.Library.ValidateToken(id_token,
                (token, claims) =>
                {
                    var claimType = Microsoft.Azure.CloudConfigurationManager.GetSetting(
                        "BlackBarLabs.Security.CredentialProvider.AzureADB2C.ClaimType");
                    var authClaims = claims.Claims
                        .Where(claim => claim.Type.CompareTo(claimType) == 0)
                        .ToArray();
                    if (authClaims.Length == 0)
                        return invalidToken($"Token does not contain claim for [{claimType}] which is necessary to operate with this sytem");
                    Guid authId;
                    if(!Guid.TryParse(authClaims[0].Value, out authId))
                        return invalidToken("User has invalid auth claim for this system");
                    return success(authId, claims.Claims.ToArray());
                },
                (why) =>
                {
                    return invalidToken(why);
                });
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

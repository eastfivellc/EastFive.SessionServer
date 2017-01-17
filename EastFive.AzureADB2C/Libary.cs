using BlackBarLabs.Extensions;
using BlackBarLabs.Web;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.AzureADB2C
{
    public static class Libary
    {
        public static async Task<TResult> InitializeAsync<TResult>(
            Uri signupConfiguration, Uri signinConfiguration, string audience,
            Func<string, string, TokenValidationParameters, TResult> onSuccess,
            Func<string, TResult> onFailed)
        {
            var requestSignup = WebRequest.CreateHttp(signupConfiguration);
            var signupEndpointDiscriminatedTask = requestSignup.GetResponseJsonAsync<
                    Resources.ConfigurationResource, BlackBarLabs.DiscriminatedDelegate<string, TResult>>(
                (config) =>
                {
                    return (callback) => callback(config.AuthorizationEndpoint);
                },
                (code, why) =>
                {
                    return (callback) => onFailed(why);
                },
                (why) =>
                {
                    return (callback) => onFailed(why);
                });
            
            var request = WebRequest.CreateHttp(signinConfiguration);
            var result = await await request.GetResponseJsonAsync(
                async (Resources.ConfigurationResource config) =>
                {
                    var signinEndpoint = config.AuthorizationEndpoint;
                    return await await GetValidator(audience, config,
                        async (validator) =>
                        {
                            var signupEndpointDiscriminated = await signupEndpointDiscriminatedTask;
                            return signupEndpointDiscriminated(
                                (signupEndpoint) =>
                                {
                                    return onSuccess(signupEndpoint, signinEndpoint, validator);
                                });
                        },
                        (why) => onFailed(why).ToTask());
                },
                (code, why) =>
                {
                    return onFailed(why).ToTask();
                },
                (why) =>
                {
                    return onFailed(why).ToTask();
                });
            return result;
        }

        private static async Task<TResult> GetValidator<TResult>(string audience, Resources.ConfigurationResource config,
            Func<TokenValidationParameters, TResult> onSuccess,
            Func<string, TResult> onFailed)
        {
            var requestKeys = WebRequest.CreateHttp(config.JwksUri);
            var result = await requestKeys.GetResponseJsonAsync(
                (Resources.KeyResource keys) =>
                {
                    var validationParameters = new TokenValidationParameters();
                    validationParameters.IssuerSigningKeys = keys.GetKeys();
                    validationParameters.ValidAudience = audience; // "51d61cbc-d8bd-4928-8abb-6e1bb315552";
                    validationParameters.ValidIssuer = config.Issuer;
                    return onSuccess(validationParameters);
                },
                (code, why) =>
                {
                    return onFailed(why);
                },
                (why) =>
                {
                    return onFailed(why);
                });
            return result;
        }
    }
}

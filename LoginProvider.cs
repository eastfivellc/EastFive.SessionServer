using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using BlackBarLabs.Web;
using BlackBarLabs.Extensions;
using System.IdentityModel.Tokens.Jwt;
using System.IdentityModel.Tokens;
using EastFive.Security.SessionServer.Api.Controllers;

namespace EastFive.Security.LoginProvider.AzureADB2C
{
    public class LoginProvider : IProvideLogin
    {
        EastFive.AzureADB2C.B2CGraphClient client = new EastFive.AzureADB2C.B2CGraphClient();
        private TokenValidationParameters validationParameters;
        internal string audience;
        private Uri signinConfiguration;
        private Uri signupConfiguration;

        public LoginProvider()
        {
            this.audience = Microsoft.Azure.CloudConfigurationManager.GetSetting(
                "EastFive.Security.LoginProvider.AzureADB2C.Audience");
            this.signinConfiguration = new Uri(Microsoft.Azure.CloudConfigurationManager.GetSetting(
                "BlackBarLabs.Security.CredentialProvider.AzureADB2C.SigninEndpoint"));
            this.signupConfiguration = new Uri(Microsoft.Azure.CloudConfigurationManager.GetSetting(
                "BlackBarLabs.Security.CredentialProvider.AzureADB2C.SignupEndpoint"));
        }
        
        public async Task InitializeAsync()
        {
            await EastFive.AzureADB2C.Libary.InitializeAsync(this.signupConfiguration, this.signinConfiguration, this.audience,
                (signupEndpoint, signinEndpoint, validationParams) =>
                {
                    AccountLinksController.SignupEndpoint = signupEndpoint;
                    AccountLinksController.SigninEndpoint = signinEndpoint;
                    AccountLinksController.Audience = this.audience;
                    this.validationParameters = validationParams;
                    return true;
                },
                (why) =>
                {
                    return false;
                });
        }

        public async Task<TResult> ValidateToken<TResult>(string idToken, 
            Func<ClaimsPrincipal, TResult> onSuccess,
            Func<string, TResult> onFailed)
        {
            if (default(TokenValidationParameters) == validationParameters)
                await InitializeAsync();
            var handler = new JwtSecurityTokenHandler();
            Microsoft.IdentityModel.Tokens.SecurityToken validatedToken;
            try
            {
                var claims = handler.ValidateToken(idToken, validationParameters, out validatedToken);
                return onSuccess(claims);
            } catch (Microsoft.IdentityModel.Tokens.SecurityTokenException ex)
            {
                return onFailed(ex.Message);
            }
        }

        public async Task<TResult> CreateLoginAsync<TResult>(string displayName,
            string userId, bool isEmail, string secret, bool forceChange,
            Func<Guid, TResult> onSuccess,
            Func<string, TResult> onFail)
        {
            var user = new EastFive.AzureADB2C.Resources.User()
            {
                DisplayName = displayName,
                AccountEnabled = true,
                SignInNames = new[] {
                    new EastFive.AzureADB2C.Resources.User.SignInName
                    {
                        Type = isEmail? "emailAddress" : "userName",
                        Value = userId,
                    }
                },
                PasswordProfile = new EastFive.AzureADB2C.Resources.User.PasswordProfileResource
                {
                    ForceChangePasswordNextLogin = forceChange,
                    Password = secret,
                },
            };
            return await client.CreateUser(user,
                onSuccess,
                onFail);
        }
        
    }
}

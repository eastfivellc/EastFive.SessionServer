using System;
using System.Linq;
using System.Net;
using System.Web.Http;
using System.Threading.Tasks;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

using Microsoft.IdentityModel.Tokens;

using BlackBarLabs.Extensions;
using BlackBarLabs.Web;
using BlackBarLabs.Api;
using System.Configuration;
using BlackBarLabs.Security.CredentialProvider.AzureADB2C;

namespace BlackBarLabs.Security.SessionServer
{
    public static class Library
    {
        private static TokenValidationParameters validationParameters;
        internal static string Audience;
        internal static string SigninEndpoint;
        internal static string SignupEndpoint;
        private static Uri SigninConfiguration;
        private static Uri SignupConfiguration;
        internal static Func<Guid, System.Security.Claims.Claim[], Task> PostAuthEvent;

        public static TResult AzureADB2CStartAsync<TResult>(this HttpConfiguration config,
                string audience, Uri configurationEndpoint,
                Func<Guid, System.Security.Claims.Claim[], Task> postAuthEvent,
            Func<TResult> onSuccess,
            Func<string, TResult> onFailed)
        {
            Library.PostAuthEvent = postAuthEvent;
            Library.Audience = Microsoft.Azure.CloudConfigurationManager.GetSetting(
                "BlackBarLabs.Security.CredentialProvider.AzureADB2C.Audience");
            Library.SigninConfiguration = new Uri(ConfigurationManager.AppSettings[
                "BlackBarLabs.Security.CredentialProvider.AzureADB2C.SigninEndpoint"]);
            Library.SignupConfiguration = new Uri(ConfigurationManager.AppSettings[
                "BlackBarLabs.Security.CredentialProvider.AzureADB2C.SignupEndpoint"]);
            //config.AddExternalControllers<SessionServer.Api.Controllers.OpenIdResponseController>();
            AddExternalControllersX<SessionServer.Api.Controllers.OpenIdResponseController>(config);
            //return InitializeAsync(audience, configurationEndpoint, onSuccess, onFailed);
            return onSuccess();
        }

        public static async Task<TResult> InitializeAsync<TResult>(
            Func<TResult> onSuccess,
            Func<string, TResult> onFailed)
        {
            var request = WebRequest.CreateHttp(Library.SigninConfiguration);
            var taskss = request.GetResponseJsonAsync(
                (ConfigurationResource config) =>
                {
                    SigninEndpoint = config.AuthorizationEndpoint;
                    return GetValidator(config, onSuccess, onFailed);
                },
                (code, why) =>
                {
                    return onFailed(why).ToTask();
                },
                (why) =>
                {
                    return onFailed(why).ToTask();
                });

            var requestSignup = WebRequest.CreateHttp(Library.SignupConfiguration);
            await requestSignup.GetResponseJsonAsync(
                (ConfigurationResource config) =>
                {
                    SignupEndpoint = config.AuthorizationEndpoint;
                    return true;
                },
                (code, why) =>
                {
                    return false;
                },
                (why) =>
                {
                    return false;
                });

            return await await taskss;
        }

        private static async Task<TResult> GetValidator<TResult>(ConfigurationResource config,
            Func<TResult> onSuccess,
            Func<string, TResult> onFailed)
        {
            var requestKeys = WebRequest.CreateHttp(config.JwksUri);
            var result = await requestKeys.GetResponseJsonAsync(
                (KeyResource keys) =>
                {
                    var validationParameters = new TokenValidationParameters();
                    validationParameters.IssuerSigningKeys = keys.GetKeys();
                    validationParameters.ValidAudience = Audience; // "51d61cbc-d8bd-4928-8abb-6e1bb315552";
                    validationParameters.ValidIssuer = config.Issuer;
                    Library.validationParameters = validationParameters;
                    return onSuccess();
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

        public static async Task<TResult> ValidateToken<TResult>(string idToken,
            Func<SecurityToken, ClaimsPrincipal, TResult> onSuccess,
            Func<string, TResult> onFailed)
        {
            if (default(TokenValidationParameters) == validationParameters)
                await InitializeAsync(
                    () => true, (why) => false);
            var handler = new JwtSecurityTokenHandler();
            Microsoft.IdentityModel.Tokens.SecurityToken validatedToken;
            try
            {
                var claims = handler.ValidateToken(idToken, validationParameters, out validatedToken);
                return onSuccess(validatedToken, claims);
            } catch (SecurityTokenException ex)
            {
                return onFailed(ex.Message);
            }
        }

        public static void AddExternalControllersX<TController>(HttpConfiguration config)
           where TController : ApiController
        {
            var routes = typeof(TController)
                .GetCustomAttributes<RoutePrefixAttribute>()
                .Select(routePrefix => routePrefix.Prefix)
                .Distinct();

            foreach (var routePrefix in routes)
            {
                config.Routes.MapHttpRoute(
                    name: routePrefix,
                    routeTemplate: routePrefix + "/{controller}/{id}",
                    defaults: new { id = RouteParameter.Optional });
            }

            //var assemblyRecognition = new InjectableAssemblyResolver(typeof(TController).Assembly,
            //    config.Services.GetAssembliesResolver());
            //config.Services.Replace(typeof(System.Web.Http.Dispatcher.IAssembliesResolver), assemblyRecognition);
        }
    }
}

using BlackBarLabs;
using BlackBarLabs.Extensions;
using BlackBarLabs.Linq;
using EastFive.Collections.Generic;
using EastFive.Security.SessionServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Security.CredentialProvider
{
    [SessionServer.Attributes.IntegrationName(InternalProvider.IntegrationName)]
    public class InternalProvider : IProvideLogin
    {
        public const string IntegrationName = "Internal";

        #region Initialization

        public const string accountIdKey = "account_id";
        
        private InternalProvider()
        {
        }
        
        public static TResult LoadFromConfig<TResult>(
            Func<InternalProvider, TResult> onLoaded,
            Func<string, TResult> onConfigurationNotAvailable)
        {
            return Web.Configuration.Settings.GetString(SessionServer.Configuration.AppSettings.OAuth.Lightspeed.ClientKey,
                (clientKey) =>
                {
                    return onLoaded(new InternalProvider());
                },
                onConfigurationNotAvailable);
        }

        [SessionServer.Attributes.IntegrationName(IntegrationName)]
        public static async Task<TResult> InitializeAsync<TResult>(
            Func<IProvideAuthorization, TResult> onProvideAuthorization,
            Func<TResult> onProvideNothing,
            Func<string, TResult> onFailure)
        {
            return await LoadFromConfig(
                (provider) => onProvideAuthorization(provider),
                (why) => onFailure(why)).ToTask();
        }

        #endregion

        #region IProvideAuthorization
        
        public Task<TResult> RedeemTokenAsync<TResult>(IDictionary<string, string> tokenParameters, 
            Func<string, Guid?, Guid?, IDictionary<string, string>, TResult> onSuccess,
            Func<Guid?, IDictionary<string, string>, TResult> onUnauthenticated,
            Func<string, TResult> onInvalidCredentials,
            Func<string, TResult> onCouldNotConnect, 
            Func<string, TResult> onUnspecifiedConfiguration, 
            Func<string, TResult> onFailure)
        {
            return onSuccess(tokenParameters[InternalProvider.accountIdKey], default(Guid?), default(Guid?), tokenParameters).ToTask();
        }
        
        #endregion

        #region IProvideLogin

        public Type CallbackController => typeof(SessionServer.Api.Controllers.IntegrationController);

        public Uri GetLoginUrl(Guid state, Uri responseControllerLocation)
        {
            return default(Uri);
        }

        public Uri GetLogoutUrl(Guid state, Uri responseControllerLocation)
        {
            return default(Uri);
        }

        public Uri GetSignupUrl(Guid state, Uri responseControllerLocation)
        {
            return default(Uri);
        }

        public Task<TResult> UserParametersAsync<TResult>(Guid actorId, System.Security.Claims.Claim[] claims, 
            IDictionary<string, string> extraParams, 
            Func<
                IDictionary<string, string>,
                IDictionary<string, Type>, 
                IDictionary<string, string>,
                TResult> onSuccess)
        {
            return onSuccess(
                new Dictionary<string, string>() { { "AutoIntegrateProducts", "Automatically Map Products" } },
                new Dictionary<string, Type>() { { "AutoIntegrateProducts", typeof(bool) } },
                new Dictionary<string, string>() { { "AutoIntegrateProducts", "When true, the system pick the best match for products when a mapping does not exists." } }).ToTask();
        }

        #endregion


    }
}

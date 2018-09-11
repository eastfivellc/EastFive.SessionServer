using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using EastFive.Linq;
using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using BlackBarLabs.Api.Resources;
using System.Web.Http.Routing;
using EastFive.Security.SessionServer;
using EastFive.Extensions;
using EastFive.Api.Azure.Credentials.Attributes;
using System.Web.Http;
using Microsoft.ApplicationInsights;
using BlackBarLabs;
using EastFive.Linq.Async;
using BlackBarLabs.Linq.Async;

namespace EastFive.Api.Azure
{
    public class AzureApplication : EastFive.Api.HttpApplication
    {
        public TelemetryClient Telemetry { get; private set; }

        public AzureApplication()
            : base()
        {
            Telemetry = EastFive.Web.Configuration.Settings.GetString(Security.SessionServer.Constants.AppSettingKeys.ApplicationInsightsKey,
                (applicationInsightsKey) => new TelemetryClient { InstrumentationKey = applicationInsightsKey },
                (why) => new TelemetryClient());

            this.AddInstigator(typeof(Security.SessionServer.Context),
                (httpApp, request, parameterInfo, onCreatedSessionContext) => onCreatedSessionContext(this.AzureContext));
        }

        protected override void Application_Start()
        {
            base.Application_Start();
        }

        protected override void Configure(HttpConfiguration config)
        {
            base.Configure(config);
            config.MessageHandlers.Add(new Api.Azure.Modules.SpaHandler(this, config));
        }
        
        private Dictionary<string, IProvideAuthorization> authorizationProviders =
            default(Dictionary<string, IProvideAuthorization>);
        public Dictionary<string, IProvideAuthorization> AuthorizationProviders { get; private set; }

        private Dictionary<string, IProvideLogin> loginProviders =
            default(Dictionary<string, IProvideLogin>);
        internal Dictionary<string, IProvideLogin> LoginProviders { get; private set; }

        private Dictionary<string, IProvideLoginManagement> credentialManagementProviders =
            default(Dictionary<string, IProvideLoginManagement>);
        internal Dictionary<string, IProvideLoginManagement> CredentialManagementProviders { get; private set; }

        protected delegate void AddProviderDelegate<TResult>(Func<Func<object, TResult>, Func<TResult>, Func<string, TResult>, Task<TResult>> initializeAsync);

        protected virtual void AddProviders<TResult>(AddProviderDelegate<TResult> callback)
        {

        }

        protected override async Task<Initialized> InitializeAsync()
        {
            var initializersTasks = new Task<object[]>[] { };
            AddProviders<object[]>(
                (providerInitializer) =>
                {
                    var temp = providerInitializer(
                        initializer => new[] { initializer },
                        () => new object[] { },
                        (why) => new object[] { });

                    initializersTasks = initializersTasks.Append(temp).ToArray();
                });
            var initializers = await initializersTasks
                    .WhenAllAsync()
                    .SelectManyAsync()
                    .ToArrayAsync();

            var credentialProviders = initializers
                .Where(
                    initializer => initializer.GetType().ContainsCustomAttribute<IntegrationNameAttribute>())
                .ToDictionary(
                    credentialProvider =>
                    {
                        var methodName = credentialProvider.GetType().GetCustomAttribute<IntegrationNameAttribute>().Name;
                        return methodName;
                    },
                    credentialProvider => credentialProvider);

            authorizationProviders = credentialProviders
                .Where(credentialProviderKvp => typeof(IProvideAuthorization).IsAssignableFrom(credentialProviderKvp.Value.GetType()))
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value as IProvideAuthorization);
            loginProviders = credentialProviders
                .Where(credentialProviderKvp => typeof(IProvideLogin).IsAssignableFrom(credentialProviderKvp.Value.GetType()))
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value as IProvideLogin);
            credentialManagementProviders = credentialProviders
                .Where(credentialProviderKvp => typeof(IProvideLoginManagement).IsAssignableFrom(credentialProviderKvp.Value.GetType()))
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value as IProvideLoginManagement);

            return await base.InitializeAsync();
        }

        internal virtual Credentials.IManageAuthorizationRequests AuthorizationRequestManager
        {
            get
            {
                return new Credentials.AzureStorageTablesLogAuthorizationRequestManager();
            }
        }

        internal TResult GetAuthorizationProvider<TResult>(string method,
            Func<IProvideAuthorization, TResult> onSuccess,
            Func<TResult> onCredintialSystemNotAvailable,
            Func<string, TResult> onFailure)
        {
            this.InitializationWait();
            if (!authorizationProviders.ContainsKey(method))
                return onCredintialSystemNotAvailable();

            var provider = authorizationProviders[method];
            return onSuccess(provider);
        }
        
        internal TResult GetLoginProvider<TResult>(Type providerType,
            Func<IProvideLogin, TResult> onSuccess,
            Func<TResult> onCredintialSystemNotAvailable,
            Func<string, TResult> onFailure)
        {
            this.InitializationWait();

            var methodName = providerType.GetCustomAttribute<IntegrationNameAttribute>().Name;
            if (!ServiceConfiguration.loginProviders.ContainsKey(methodName))
                return onCredintialSystemNotAvailable();

            var provider = ServiceConfiguration.loginProviders[methodName];
            return onSuccess(provider);
        }
        
        public virtual Task<TResult> OnUnmappedUserAsync<TResult>(string method, IProvideAuthorization authorizationProvider, string subject, IDictionary<string, string> extraParameters, 
            Func<Guid, TResult> onCreatedMapping,
            Func<TResult> onNoChange)
        {
            return onNoChange().ToTask();
        }

        public virtual Web.Services.ISendMessageService SendMessageService { get => Web.Services.ServiceConfiguration.SendMessageService(); }
        
        public virtual Web.Services.ITimeService TimeService { get => Web.Services.ServiceConfiguration.TimeService(); }
        
        internal virtual WebId GetActorLink(Guid actorId, UrlHelper url)
        {
            return Security.SessionServer.Library.configurationManager.GetActorLink(actorId, url);
        }

        public virtual async Task<TResult> GetRedirectUriAsync<TResult>(
                IProvideAuthorization authorizationProvider,
                string validationType,
                AuthenticationActions action,
                Guid requestId,
                Guid? authorizationId,
                string token, string refreshToken,
                IDictionary<string, string> authParams,
                Uri baseUri,
                Uri redirectUriFromPost,
            Func<Uri, TResult> onSuccess,
            Func<string, string, TResult> onInvalidParameter,
            Func<string, TResult> onFailure)
        {
            if(authorizationProvider is Credentials.IProvideRedirection)
            {
                return await (authorizationProvider as Credentials.IProvideRedirection).GetRedirectUriAsync(this, authorizationId, requestId, token, refreshToken,
                    authParams, 
                    (redirectUri) => onSuccess(this.SetRedirectParameters(new Uri(baseUri, redirectUri), requestId, authorizationId, token, refreshToken)),
                    onInvalidParameter,
                    onFailure);
            }

            if (!redirectUriFromPost.IsDefault())
            {
                var redirectUrl = SetRedirectParameters(redirectUriFromPost, requestId, authorizationId, token, refreshToken);
                return onSuccess(redirectUrl);
            }

            if (null != authParams && authParams.ContainsKey(Security.SessionServer.Configuration.AuthorizationParameters.RedirectUri))
            {
                Uri redirectUri;
                var redirectUriString = authParams[Security.SessionServer.Configuration.AuthorizationParameters.RedirectUri];
                if (!Uri.TryCreate(redirectUriString, UriKind.Absolute, out redirectUri))
                    return onInvalidParameter("REDIRECT", $"BAD URL in redirect call:{redirectUriString}");
                var redirectUrl = SetRedirectParameters(redirectUri, requestId, authorizationId, token, refreshToken);
                return onSuccess(redirectUrl);
            }

            return await EastFive.Web.Configuration.Settings.GetUri(
                EastFive.Security.SessionServer.Configuration.AppSettings.LandingPage,
                (redirectUri) =>
                {
                    var redirectUrl = SetRedirectParameters(redirectUri, requestId, authorizationId, token, refreshToken);
                    return onSuccess(redirectUrl);
                },
                (why) => onFailure(why)).ToTask();
        }

        protected Uri SetRedirectParameters(Uri redirectUri, Guid requestId, Guid? authorizationId, string token, string refreshToken)
        {
            var redirectUrl = redirectUri
                //.SetQueryParam(parameterAuthorizationId, authorizationId.Value.ToString("N"))
                //.SetQueryParam(parameterToken, token)
                //.SetQueryParam(parameterRefreshToken, refreshToken)
                .SetQueryParam("request_id", requestId.ToString());
            return redirectUrl;
        }

        public Security.SessionServer.Context AzureContext
        {
            get
            {
                return new EastFive.Security.SessionServer.Context(
                    () => new EastFive.Security.SessionServer.Persistence.DataContext(
                        EastFive.Security.SessionServer.Configuration.AppSettings.Storage));
            }
        }
        
        public TResult StoreMonitoring<TResult>(
            Func<StoreMonitoringDelegate, TResult> onMonitorUsingThisCallback,
            Func<TResult> onNoMonitoring)
        {
            StoreMonitoringDelegate callback = (monitorRecordId, authenticationId, when, method, controllerName, queryString) =>
                EastFive.Api.Azure.Monitoring.MonitoringDocument.CreateAsync(monitorRecordId, authenticationId,
                        when, method, controllerName, queryString, 
                        AzureContext.DataContext.AzureStorageRepository,
                        () => true);
            return onMonitorUsingThisCallback(callback);
        }


    }
}

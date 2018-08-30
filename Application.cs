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

namespace EastFive.Api.Azure
{
    public class Application : EastFive.Api.HttpApplication
    {
        public Application()
            : base()
        {
            this.AddInstigator(typeof(Security.SessionServer.Context),
                (httpApp, request, parameterInfo, onCreatedSessionContext) => onCreatedSessionContext(this.AzureContext));

        }

        protected override void Application_Start()
        {
            base.Application_Start();
        }

        Task<object[]> initializationChain = (new object[] { }).ToTask();

        private Dictionary<string, IProvideAuthorization> authorizationProviders =
            default(Dictionary<string, IProvideAuthorization>);
        internal Dictionary<string, IProvideAuthorization> AuthorizationProviders { get; private set; }

        private Dictionary<string, IProvideLogin> loginProviders =
            default(Dictionary<string, IProvideLogin>);
        internal Dictionary<string, IProvideLogin> LoginProviders { get; private set; }

        private Dictionary<string, IProvideLoginManagement> credentialManagementProviders =
            default(Dictionary<string, IProvideLoginManagement>);
        internal Dictionary<string, IProvideLoginManagement> CredentialManagementProviders { get; private set; }

        protected override async Task<Initialized> InitializeAsync()
        {
            var initializers = await initializationChain;
            
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

        internal TResult GetAuthorizationProvider<TResult>(string method,
            Func<IProvideAuthorization, TResult> onSuccess,
            Func<TResult> onCredintialSystemNotAvailable,
            Func<string, TResult> onFailure)
        {
            this.InitializationWait();
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

        protected void AddProvider(Func<Func<object, object[]>, Func<object[]>, Func<string, object[]>, Task<object[]>> initializeAsync)
        {
            var initializersTask = initializationChain;
            initializationChain = Task.Run<object[]>(
                async () =>
                    {
                        var initializers = await initializersTask;
                        return await initializeAsync(
                            initializer => initializers.Append(initializer).ToArray(),
                            () => initializers,
                            (why) => initializers);
                    },
                System.Threading.CancellationToken.None);

        }

        public virtual Web.Services.ISendMessageService SendMessageService { get => Web.Services.ServiceConfiguration.SendMessageService(); }
        
        public virtual Web.Services.ITimeService TimeService { get => Web.Services.ServiceConfiguration.TimeService(); }
        
        internal virtual WebId GetActorLink(Guid actorId, UrlHelper url)
        {
            return Security.SessionServer.Library.configurationManager.GetActorLink(actorId, url);
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

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
using EastFive.Api.Controllers;
using EastFive.Collections.Generic;
using System.Linq.Expressions;

namespace EastFive.Api.Azure
{
    public class AzureApplication : EastFive.Api.HttpApplication
    {
        public const string QueryRequestIdentfier = "request_id";

        public TelemetryClient Telemetry { get; private set; }

        public AzureApplication()
            : base()
        {
            Telemetry = EastFive.Web.Configuration.Settings.GetString(EastFive.Azure.AppSettings.ApplicationInsightsKey,
                (applicationInsightsKey) => new TelemetryClient { InstrumentationKey = applicationInsightsKey },
                (why) => new TelemetryClient());

            this.AddInstigator(typeof(Security.SessionServer.Context),
                (httpApp, request, parameterInfo, onCreatedSessionContext) => onCreatedSessionContext(this.AzureContext));
        }

        public virtual async Task<bool> CanAdministerCredentialAsync(Guid actorInQuestion, Api.Controllers.Security security)
        {
            if (actorInQuestion == security.performingAsActorId)
                return true;

            if (await IsAdminAsync(security))
                return true;

            return false;
        }

        public virtual Task<bool> IsAdminAsync(Api.Controllers.Security security)
        {
            return EastFive.Web.Configuration.Settings.GetGuid(
                EastFive.Api.AppSettings.ActorIdSuperAdmin,
                (actorIdSuperAdmin) =>
                {
                    if (actorIdSuperAdmin == security.performingAsActorId)
                        return true;

                    return false;
                },
                (why) => false).AsTask();
        }

        protected override void Configure(HttpConfiguration config)
        {
            base.Configure(config);
            config.MessageHandlers.Add(new Api.Azure.Modules.SpaHandler(this, config));
        }
        
        public IDictionaryAsync<string, IProvideAuthorization> AuthorizationProviders
        {
            get
            {
                return this.InstantiateAll<IProvideAuthorization>()
                    .Where(authorization => !authorization.IsDefaultOrNull())
                    .Select(authorization => authorization.PairWithKey(authorization.Method))
                    .ToDictionary();
            }
        }

        public IDictionaryAsync<string, IProvideLogin> LoginProviders
        {
            get
            {
                return this.InstantiateAll<IProvideLogin>()
                    .Where(loginProvider => !loginProvider.IsDefaultOrNull())
                    .Select(loginProvider => loginProvider.PairWithKey(loginProvider.Method))
                    .ToDictionary();
            }
        }

        public IDictionaryAsync<string, IProvideLoginManagement> CredentialManagementProviders
        {
            get
            {
                return this.InstantiateAll<IProvideLoginManagement>()
                    .Where(loginManager => !loginManager.IsDefaultOrNull())
                    .Select(loginManager => loginManager.PairWithKey(loginManager.Method))
                    .ToDictionary();
            }
        }
        
        protected override async Task<Initialized> InitializeAsync()
        {
            return await base.InitializeAsync();
        }

        internal virtual Credentials.IManageAuthorizationRequests AuthorizationRequestManager
        {
            get
            {
                return new Credentials.AzureStorageTablesLogAuthorizationRequestManager();
            }
        }

        internal async Task<TResult> GetAuthorizationProviderAsync<TResult>(string method,
            Func<IProvideAuthorization, TResult> onSuccess,
            Func<TResult> onCredentialSystemNotAvailable,
            Func<string, TResult> onFailure)
        {
            return await this.AuthorizationProviders.TryGetValue(method,
                onSuccess,
                onCredentialSystemNotAvailable);
        }
        
        internal async Task<TResult> GetLoginProviderAsync<TResult>(string method,
            Func<IProvideLogin, TResult> onSuccess,
            Func<TResult> onCredentialSystemNotAvailable,
            Func<string, TResult> onFailure)
        {
            // this.InitializationWait();
            return await this.LoginProviders.TryGetValue(method,
                onSuccess,
                onCredentialSystemNotAvailable);
        }
        
        public virtual async Task<TResult> OnUnmappedUserAsync<TResult>(string method, IProvideAuthorization authorizationProvider, string subject, IDictionary<string, string> extraParameters, 
            Func<Guid, TResult> onCreatedMapping,
            Func<TResult> onNoChange)
        {
            if (authorizationProvider is Credentials.IProvideAccountInformation)
            {
                return await(authorizationProvider as Credentials.IProvideAccountInformation)
                    .CreateAccount(this, method, authorizationProvider, subject, extraParameters,
                        onCreatedMapping,
                        onNoChange);
            }
            return onNoChange();
        }

        public virtual Web.Services.ISendMessageService SendMessageService { get => Web.Services.ServiceConfiguration.SendMessageService(); }
        
        public virtual Web.Services.ITimeService TimeService { get => Web.Services.ServiceConfiguration.TimeService(); }
        
        internal virtual WebId GetActorLink(Guid actorId, UrlHelper url)
        {
            return Security.SessionServer.Library.configurationManager.GetActorLink(actorId, url);
        }

        internal virtual Task<TResult> GetActorNameDetailsAsync<TResult>(Guid actorId,
            Func<string, string, string, TResult> onActorFound,
            Func<TResult> onActorNotFound)
        {
            return Security.SessionServer.Library.configurationManager.GetActorNameDetailsAsync(actorId, onActorFound, onActorNotFound);
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
            if(!(authorizationProvider is Credentials.IProvideRedirection))
                return await ComputeRedirect(requestId, authorizationId, token, refreshToken, redirectUriFromPost, authParams,
                        onSuccess,
                        onInvalidParameter,
                        onFailure);

            return await await (authorizationProvider as Credentials.IProvideRedirection).GetRedirectUriAsync(this, authorizationId, requestId, token, refreshToken,
                        authParams, 
                    (redirectUri) =>
                    {
                        var fullUri = new Uri(baseUri, redirectUri);
                        var redirectDecorated = this.SetRedirectParameters(fullUri, requestId, authorizationId, token, refreshToken);
                        return onSuccess(redirectDecorated).AsTask();
                    },
                    () => ComputeRedirect(requestId, authorizationId, token, refreshToken, redirectUriFromPost, authParams,
                        onSuccess,
                        onInvalidParameter,
                        onFailure),
                    onInvalidParameter.AsAsyncFunc(),
                    onFailure.AsAsyncFunc());
            
        }

        private async Task<TResult> ComputeRedirect<TResult>(
                Guid requestId,
                Guid? authorizationId,
                string token, string refreshToken,
                Uri redirectUriFromPost,
                IDictionary<string, string> authParams,
            Func<Uri, TResult> onSuccess,
            Func<string, string, TResult> onInvalidParameter,
            Func<string, TResult> onFailure)
        {
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
                .SetQueryParam(AzureApplication.QueryRequestIdentfier, requestId.ToString());
            return redirectUrl;
        }

        public Security.SessionServer.Context AzureContext
        {
            get
            {
                return new EastFive.Security.SessionServer.Context(
                    () => new EastFive.Security.SessionServer.Persistence.DataContext(
                        EastFive.Azure.AppSettings.ASTConnectionStringKey));
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

        public delegate Task<HttpResponseMessage> ExecuteAsyncDelegate(DateTime whenRequested, Action<double, string> updateProgress);

        private class ExecuteAsyncWrapper : IExecuteAsync
        {
            public DateTime when;
            public Expression<ExecuteAsyncDelegate> callback { get; set; }

            public Task<HttpResponseMessage> InvokeAsync(Action<double> updateCallback)
            {
                return callback.Compile().Invoke(
                    when,
                    (progress, msg) =>
                    {
                        updateCallback(progress);
                    });
            }
        }

        public IExecuteAsync ExecuteBackground(
            Expression<ExecuteAsyncDelegate> callback)
        {
            var wrapper = new ExecuteAsyncWrapper();
            wrapper.callback = callback;
            wrapper.when = DateTime.UtcNow;
            return wrapper;
        }
    }
}

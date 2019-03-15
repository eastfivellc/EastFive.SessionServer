using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http.Routing;
using BlackBarLabs.Api.Resources;
using BlackBarLabs.Extensions;
using EastFive.Api;
using EastFive.Api.Controllers;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Persistence;
using Newtonsoft.Json;

namespace EastFive.Azure.Auth
{
    [DataContract]
    [FunctionViewController4(
        Route = "XSession",
        Resource = typeof(Session),
        ContentType = "x-application/auth-session",
        ContentTypeVersion = "0.1")]
    public struct Session : IReferenceable
    {
        public Guid id => sessionId.id;

        public const string SessionIdPropertyName = "id";
        [ApiProperty(PropertyName = SessionIdPropertyName)]
        [JsonProperty(PropertyName = SessionIdPropertyName)]
        [StorageProperty(IsRowKey = true, Name = SessionIdPropertyName)]
        public IRef<Session> sessionId;

        public const string AuthorizationPropertyName = "authorization";
        [ApiProperty(PropertyName = AuthorizationPropertyName)]
        [JsonProperty(PropertyName = AuthorizationPropertyName)]
        [StorageProperty(Name = AuthorizationPropertyName)]
        public IRefOptional<Authorization> authorization { get; set; }

        public const string AccountPropertyName = "account";
        [JsonProperty(PropertyName = AccountPropertyName)]
        [StorageProperty(Name = AccountPropertyName)]
        public Guid? account { get; set; }

        public const string HeaderNamePropertyName = "header_name";
        [ApiProperty(PropertyName = HeaderNamePropertyName)]
        [JsonProperty(PropertyName = HeaderNamePropertyName)]
        public string HeaderName
        {
            get
            {
                return "Authorization";
            }
            set
            {

            }
        }
        
        public const string TokenPropertyName = "token";
        [ApiProperty(PropertyName = TokenPropertyName)]
        [JsonProperty(PropertyName = TokenPropertyName)]
        public string token;
        
        public const string RefreshTokenPropertyName = "refresh_token";
        [ApiProperty(PropertyName = RefreshTokenPropertyName)]
        [JsonProperty(PropertyName = RefreshTokenPropertyName)]
        [StorageProperty(Name = RefreshTokenPropertyName)]
        public string refreshToken;

        private static async Task<IDictionary<string, string>> GetClaimsAsync(
            Api.Azure.AzureApplication application, IRefOptional<Authorization> authorizationRefMaybe)
        {
            if (!authorizationRefMaybe.HasValue)
                return new Dictionary<string, string>();
            var authorizationRef = authorizationRefMaybe.Ref;

            return await EastFive.Web.Configuration.Settings.GetString(
                EastFive.Api.AppSettings.ActorIdClaimType,
                (accountIdClaimType) =>
                {
                    return GetSessionAcountAsync(authorizationRef, application,
                        (accountId) =>
                            new Dictionary<string, string>()
                            {
                                { accountIdClaimType, accountId.ToString() }
                            },
                        (why) => new Dictionary<string, string>());
                },
                (why) => (new Dictionary<string, string>()).AsTask());
        }

        [HttpPost] //(MatchAllBodyParameters = false)]
        public async static Task<HttpResponseMessage> CreateAsync(
                [Property(Name = SessionIdPropertyName)]IRef<Session> sessionId,
                [PropertyOptional(Name = AuthorizationPropertyName)]IRefOptional<Authorization> authorizationRefMaybe,
                [Resource]Session session,
                Api.Azure.AzureApplication application,
            CreatedBodyResponse<Session> onCreated,
            AlreadyExistsResponse onAlreadyExists,
            ForbiddenResponse forbidden,
            ConfigurationFailureResponse onConfigurationFailure)
        {
            session.refreshToken = Security.SecureGuid.Generate().ToString("N");
            
            return await await session.StorageCreateAsync(
                (sessionIdCreated) =>
                {
                    return Web.Configuration.Settings.GetUri(
                            EastFive.Security.AppSettings.TokenScope,
                        async scope =>
                        {
                            var claims = await GetClaimsAsync(application, authorizationRefMaybe);
                            return BlackBarLabs.Security.Tokens.JwtTools.CreateToken(sessionId.id,
                                scope, TimeSpan.FromDays(365.0), claims, // TODO: Expiration time from .Config
                                (tokenNew) =>
                                {
                                    session.token = tokenNew;
                                    return onCreated(session);
                                },
                                (missingConfig) => onConfigurationFailure("Missing", missingConfig),
                                (configName, issue) => onConfigurationFailure(configName, issue));
                        },
                        (why) => onConfigurationFailure("Missing", why).AsTask());
                },
                () => onAlreadyExists().AsTask());
        }

        private static async Task<TResult> GetSessionAcountAsync<TResult>(IRef<Authorization> authorizationRef,
                Api.Azure.AzureApplication application,
            Func<Guid, TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            return await await authorizationRef.StorageGetAsync(
                async (authorization) =>
                {
                    var methodRef = authorization.Method;
                    return await await Authentication.ById(methodRef, application,
                        async method =>
                        {
                            var authorizationKey = await method.GetAuthorizationKeyAsync(application, authorization.parameters);
                            return await Auth.AccountMapping.FindByMethodAndKeyAsync(method.authenticationId, authorizationKey,
                                    authorization,
                                accountId => onSuccess(accountId),
                                () => onFailure("No mapping to that account."));
                        },
                        () =>
                        {
                            return CheckSuperAdminBeforeFailure(authorizationRef,
                                    "Authorization method is no longer valid on this system.",
                                onSuccess, onFailure).AsTask();
                        });
                },
                () =>
                {
                    return CheckSuperAdminBeforeFailure(authorizationRef, "Authorization not found.",
                        onSuccess, onFailure).AsTask();
                });
          
        }

        private static TResult CheckSuperAdminBeforeFailure<TResult>( 
                IRef<Authorization> authorizationRef, string failureMessage,
            Func<Guid, TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            var isSuperAdminAuth = Web.Configuration.Settings.GetGuid(EastFive.Api.AppSettings.AuthorizationIdSuperAdmin,
                (authorizationIdSuperAdmin) =>
                {
                    if (authorizationIdSuperAdmin == authorizationRef.id)
                        return true;
                    return false;
                },
                why => false);

            if (!isSuperAdminAuth)
                return onFailure(failureMessage);

            return Web.Configuration.Settings.GetGuid(EastFive.Api.AppSettings.ActorIdSuperAdmin,
                (authorizationIdSuperAdmin) => onSuccess(authorizationIdSuperAdmin),
                (dontCare) => onFailure(failureMessage));
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using BlackBarLabs.Api.Resources;
using BlackBarLabs.Extensions;
using EastFive.Api;
using EastFive.Api.Controllers;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Collections.Generic;
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

        public const string SessionIdPropertyName = "session_id";
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

        [HttpPost] //(MatchAllBodyParameters = false)]
        public async static Task<HttpResponseMessage> CreateAsync(
                [Property(Name = SessionIdPropertyName)]IRef<Session> sessionId,
                [PropertyOptional(Name = AuthorizationPropertyName)]IRefOptional<Session> authorization,
                [Resource]Session session,
            CreatedBodyResponse onCreated,
            AlreadyExistsResponse onAlreadyExists,
            ForbiddenResponse forbidden,
            ConfigurationFailureResponse onConfigurationFailure)
        {
            session.refreshToken = Security.SecureGuid.Generate().ToString("N");
            // Null out the account in case it somehow got assigned.
            session.account = default(Guid?);
            if (authorization.HasValue)
            {
                // TODO: Go dig up the account;
            }

            return await session.StorageCreateAsync(
                (sessionIdCreated) =>
                {
                    var claims = new Dictionary<string, string>();
                    return EastFive.Web.Configuration.Settings.GetString(
                        EastFive.Api.AppSettings.ActorIdClaimType,
                        (accountIdClaimType) =>
                        {
                            if (session.account.HasValue)
                            {
                                var accountId = session.account.Value;
                                claims.AddOrReplace(accountIdClaimType, accountId.ToString());
                            }
                            return Web.Configuration.Settings.GetUri(
                                EastFive.Security.AppSettings.TokenScope,
                                scope =>
                                {
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
                                (why) => onConfigurationFailure("Missing", why));
                        },
                        (why) => onConfigurationFailure("Missing", why));
                },
                () => onAlreadyExists());
        }
    }
}
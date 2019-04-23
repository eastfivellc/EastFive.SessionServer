using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using BlackBarLabs.Api;
using EastFive.Api;
using EastFive.Api.Azure;
using EastFive.Api.Controllers;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;
using Newtonsoft.Json;

namespace EastFive.Azure.Auth
{
    [FunctionViewController4(
        Route = "RedirectionManager",
        Resource = typeof(RedirectionManager),
        ContentType = "x-application/auth-redirection-manager",
        ContentTypeVersion = "0.1")]
    public struct RedirectionManager
    {
        [JsonIgnore]
        public Guid id => redirectionManagerRef.id;

        public const string RedirectionManagerId = "id";
        [ApiProperty(PropertyName = RedirectionManagerId)]
        [JsonProperty(PropertyName = RedirectionManagerId)]
        [RowKey]
        [StandardParititionKey]
        public IRef<RedirectionManager> redirectionManagerRef;

        public const string AuthorizationPropertyName = "authorization";
        [ApiProperty(PropertyName = AuthorizationPropertyName)]
        [JsonProperty(PropertyName = AuthorizationPropertyName)]
        [Storage]
        public IRefOptional<Authorization> authorization { get; set; }

        public const string Message = "message";
        [ApiProperty(PropertyName = Message)]
        [JsonProperty(PropertyName = Message)]
        [Storage]
        public string message { get; set; }

        public const string When = "when";
        [ApiProperty(PropertyName = When)]
        [JsonProperty(PropertyName = When)]
        [Storage]
        public DateTime when { get; set; }

        public const string Redirection = "redirection";
        [ApiProperty(PropertyName = Redirection)]
        [JsonProperty(PropertyName = Redirection)]
        [Storage]
        public Uri redirection { get; set; }

        [Api.HttpGet]
        public static Task<HttpResponseMessage> GetAllSecureAsync(
                [QueryParameter(Name = "ApiKeySecurity")]string apiSecurityKey,
                [QueryParameter(Name = "method")]IRef<Method> methodRef,
                ApiSecurity apiSecurity,
                AzureApplication application,
                HttpRequestMessage request,
            MultipartResponseAsync<RedirectionManager> onContent)
        {
            Expression<Func<Authorization, bool>> allQuery =
                   (authorization) => true;
            var redirections = allQuery
                .StorageQuery()
                .Where(authorization => !authorization.Method.IsDefaultOrNull())
                .Where(authorization => authorization.Method.id == methodRef.id)
                .Select(
                    async authorization =>
                    {
                        return await await Method.ById(authorization.Method, application,
                            async method =>
                            {
                                return await await method.ParseTokenAsync(authorization.parameters, application,
                                    (externalId, authRefDiscard, loginProvider) =>
                                    {
                                        return Auth.Redirection.ProcessAsync(authorization,
                                                async updatedAuth =>
                                                {

                                                }, method, externalId, authorization.parameters,
                                                Guid.NewGuid(), request.RequestUri, application, loginProvider,
                                            (uri, obj) =>
                                            {
                                                return new RedirectionManager
                                                {
                                                    when = authorization.lastModified,
                                                    message = $"Ready:{externalId}",
                                                    authorization = authorization.authorizationRef.Optional(),
                                                    redirection = uri,
                                                };
                                            },
                                            (why) => new RedirectionManager { authorization = authorization.authorizationRef.Optional(), message = why },
                                            (why) => new RedirectionManager { authorization = authorization.authorizationRef.Optional(), message = why },
                                            application.Telemetry);
                                    },
                                    why => (new RedirectionManager { authorization = authorization.authorizationRef.Optional(), message = why }).AsTask());
                            },
                            () => new RedirectionManager()
                            {
                                authorization = authorization.authorizationRef.Optional(),
                                message = "Method no longer supported",
                            }.AsTask());
                    })
                .Parallel();
            return onContent(redirections);
        }

        [Api.HttpGet]
        public static async Task<HttpResponseMessage> GetAllSecureAsync(
                [QueryParameter(Name = "ApiKeySecurity")]string apiSecurityKey,
                [QueryParameter(Name = "authorization")]IRef<Authorization> authorizationRef,
                ApiSecurity apiSecurity,
                AzureApplication application,
                HttpRequestMessage request,
            MultipartResponseAsync<Authorization> onContent,
            RedirectResponse onSuccess,
            NotFoundResponse onNotFound,
            ForbiddenResponse onFailure)
        {
            return await await authorizationRef.StorageGetAsync(
                async authorization =>
                {
                    return await await Method.ById(authorization.Method, application,
                        async method =>
                        {
                            return await await method.ParseTokenAsync(authorization.parameters, application,
                                (externalId, authRefDiscard, loginProvider) =>
                                {
                                    return Auth.Redirection.ProcessAsync(authorization,
                                            async updatedAuth =>
                                            {

                                            }, method, externalId, authorization.parameters,
                                            Guid.NewGuid(), request.RequestUri, application, loginProvider,
                                        (uri, obj) => onSuccess(uri, obj.IsDefaultOrNull() ? string.Empty : obj.ToString()),
                                        (why) => onFailure().AddReason(why),
                                        (why) => onFailure().AddReason(why),
                                        application.Telemetry);
                                },
                                why => onFailure().AddReason(why).AsTask());
                        },
                        () => onFailure().AddReason("Method no longer supported").AsTask());
                },
                () => onNotFound().AsTask());
        }
    }
}

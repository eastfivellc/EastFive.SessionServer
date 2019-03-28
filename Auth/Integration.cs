using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http.Routing;
using BlackBarLabs.Api;
using EastFive.Api;
using EastFive.Api.Controllers;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;
using EastFive.Security;
using EastFive.Serialization;
using Newtonsoft.Json;

namespace EastFive.Azure.Auth
{
    [DataContract]
    [FunctionViewController4(
        Route = "Integration",
        Resource = typeof(Integration),
        ContentType = "x-application/auth-integration",
        ContentTypeVersion = "0.1")]
    [StorageTable]
    public struct Integration : IReferenceable
    {
        [JsonIgnore]
        public Guid id => integrationId;

        public const string AccountMappingIdPropertyName = "id";
        [ApiProperty(PropertyName = AccountMappingIdPropertyName)]
        [JsonProperty(PropertyName = AccountMappingIdPropertyName)]
        [Storage]
        public Guid integrationId;

        [RowKey]
        [StandardParititionKey]
        [JsonIgnore]
        public IRef<Integration> mappingId
        {
            get
            {
                var composeId = this.Method.id
                    .ComposeGuid(this.accountId);
                return new Ref<Integration>(composeId);
            }
            set
            {
            }
        }

        public const string MethodPropertyName = "method";
        [JsonIgnore]
        [Storage(Name = MethodPropertyName)]
        public IRef<Method> Method { get; set; }

        public const string AccountPropertyName = "account";
        [ApiProperty(PropertyName = AccountPropertyName)]
        [JsonProperty(PropertyName = AccountPropertyName)]
        [Storage(Name = AccountPropertyName)]
        public Guid accountId { get; set; }

        [StorageTable]
        public struct IntegrationLookup : IReferenceable
        {
            [JsonIgnore]
            public Guid id => integrationLookupId.id;

            [RowKey]
            [StandardParititionKey]
            [JsonIgnore]
            public IRef<IntegrationLookup> integrationLookupId
            {
                get
                {
                    return GetLookup(this.Method, this.accountId);
                }
                set
                {
                }
            }

            public const string AccountKeyPropertyName = "account";
            [JsonIgnore]
            [Storage]
            public Guid accountId { get; set; }

            public const string MethodPropertyName = "method";
            [JsonIgnore]
            [Storage]
            public IRef<Method> Method { get; set; }

            [JsonIgnore]
            [Storage]
            public IRef<Integration> integrationId;

            public static IRef<IntegrationLookup> GetLookup(
                IRef<Method> method, Guid accountId)
            {
                var composeId = method.id
                    .ComposeGuid(accountId);
                return new Ref<IntegrationLookup>(composeId);
            }
        }

        [StorageTable]
        public struct AuthorizationIntegrationLookup : IReferenceable
        {
            [JsonIgnore]
            public Guid id => authorizationLookupRef.id;

            [RowKey]
            [StandardParititionKey]
            [JsonIgnore]
            public IRef<Authorization> authorizationLookupRef;

            [JsonIgnore]
            [Storage]
            public IRef<Integration> integrationMappingRef;
        }

        public const string AuthorizationPropertyName = "authorization";
        [ApiProperty(PropertyName = AuthorizationPropertyName)]
        [JsonProperty(PropertyName = AuthorizationPropertyName)]
        [Storage(Name = AuthorizationPropertyName)]
        public IRefOptional<Authorization> authorization { get; set; }

        [Storage]
        public IRefOptional<IntegrationLookup> integrationLookup { get; set; }

        [Api.HttpPost] //(MatchAllBodyParameters = false)]
        public async static Task<HttpResponseMessage> CreateAsync(
                [Property(Name = AccountPropertyName)]Guid accountId,
                [Property(Name = AuthorizationPropertyName)]IRef<Authorization> authorizationRef,
                [Resource]Integration integration,
                Api.Azure.AzureApplication application, EastFive.Api.Controllers.Security security,
            CreatedResponse onCreated,
            ForbiddenResponse forbidden,
            ReferencedDocumentDoesNotExistsResponse<Authorization> onAuthenticationDoesNotExist,
            GeneralConflictResponse onFailure)
        {
            if (!await application.CanAdministerCredentialAsync(accountId, security))
                return forbidden();

            return await await authorizationRef.StorageGetAsync(
                async authorization =>
                {
                    integration.Method = authorization.Method; // method is used in the .mappingId
                    var authorizationLookup = new AuthorizationIntegrationLookup
                    {
                        integrationMappingRef = integration.mappingId,
                        authorizationLookupRef = authorizationRef,
                    };
                    return await await authorizationLookup.StorageCreateAsync(
                        async (idDiscard) =>
                        {
                            integration.integrationLookup = await await authorization.ParseCredentailParameters(
                                    application,
                                (accountKey, loginProvider) =>
                                {
                                    var lookup = new IntegrationLookup()
                                    {
                                        accountId = accountId,
                                        integrationId = integration.mappingId,
                                        Method = authorization.Method,
                                    };
                                    return lookup.StorageCreateAsync(
                                        (discard) => new RefOptional<IntegrationLookup>(
                                            lookup.integrationLookupId),
                                        () => new RefOptional<IntegrationLookup>());
                                },
                                (why) =>
                                {
                                    var amLookupMaybe = new RefOptional<IntegrationLookup>();
                                    return amLookupMaybe.AsTask();
                                });
                            return await integration.StorageCreateAsync(
                                createdId =>
                                {
                                    return onCreated();
                                },
                                () =>
                                {
                                    return forbidden().AddReason("Account is already mapped to that authentication.");
                                });
                        },
                        () => onFailure("Authorization is already mapped to another account.").AsTask());
                },
                () => onAuthenticationDoesNotExist().AsTask());
        }

        public static async Task<TResult> CreateByMethodAndKeyAsync<TResult>(IRef<Method> method, 
                Guid internalAccountId, IDictionary<string, string> parameters,
            Func<TResult> onCreated,
            Func<string, TResult> onFailure)
        {
            var authorization = new Authorization
            {
                authorizationId = new Ref<Authorization>(Guid.NewGuid()),
                parameters = parameters,
                Method = method,
            };
            return await await authorization.StorageCreateAsync<Authorization, Task<TResult>>(
                async (discardId) =>
                {
                    var integration = new Integration()
                    {
                        integrationId = Guid.NewGuid(),
                        accountId  = internalAccountId,
                        Method = method,
                        authorization = new RefOptional<Authorization>(authorization.authorizationId),
                    };
                    integration.Method = authorization.Method; // method is used in the .mappingId
                    var authorizationLookup = new AuthorizationIntegrationLookup
                    {
                        integrationMappingRef = integration.mappingId,
                        authorizationLookupRef = authorization.authorizationId,
                    };
                    return await await authorizationLookup.StorageCreateAsync(
                        async (idDiscard) =>
                        {
                            var lookup = new IntegrationLookup()
                            {
                                accountId = internalAccountId,
                                integrationId = integration.mappingId,
                                Method = authorization.Method,
                            };
                            integration.integrationLookup = await lookup.StorageCreateAsync(
                                (discard) => new RefOptional<IntegrationLookup>(
                                    lookup.integrationLookupId),
                                () => new RefOptional<IntegrationLookup>());

                            return await integration.StorageCreateAsync(
                                createdId =>
                                {
                                    return onCreated();
                                },
                                () => throw new Exception("Guid not unique"));
                        },
                        () => onFailure("Authorization is already mapped to another account.").AsTask());
                },
                () => throw new Exception("Guid not unique"));
        }

        public static async Task<TResult> GetParametersByAccountIdAsync<TResult>(IRef<Method> authenticationId, Guid accountId,
            Func<Integration, IDictionary<string, string>, TResult> onFound,
            Func<TResult> onNotFound)
        {
            var lookupRef = IntegrationLookup.GetLookup(authenticationId, accountId);
            return await await lookupRef.StorageGetAsync(
                async lookup =>
                {
                    return await await lookup.integrationId.StorageGetAsync(
                        integration =>
                        {
                            return integration.authorization.StorageGetAsync(
                                authorization => onFound(integration, authorization.parameters),
                                () => onNotFound());
                        },
                        onNotFound.AsAsyncFunc());
                },
                onNotFound.AsAsyncFunc());
        }
    }
}
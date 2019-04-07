using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http.Routing;
using BlackBarLabs.Api;
using EastFive;
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
        public Guid id => integrationRef.id;

        public const string IntegrationIdPropertyName = "id";
        [ApiProperty(PropertyName = IntegrationIdPropertyName)]
        [JsonProperty(PropertyName = IntegrationIdPropertyName)]
        [RowKey]
        [StandardParititionKey]
        public IRef<Integration> integrationRef;
        
        [JsonIgnore]
        [Storage]
        public IRef<Method> Method { get; set; }

        public const string AccountPropertyName = "account";
        [ApiProperty(PropertyName = AccountPropertyName)]
        [JsonProperty(PropertyName = AccountPropertyName)]
        [Storage(Name = AccountPropertyName)]
        public Guid accountId { get; set; }
        
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

        [StorageTable]
        public struct AccountIntegrationLookup : IReferenceable
        {
            [JsonIgnore]
            public Guid id => accountIntegrationLookupRef.id;

            [RowKey]
            [StandardParititionKey]
            [JsonIgnore]
            public IRef<AccountIntegrationLookup> accountIntegrationLookupRef;

            [JsonIgnore]
            [Storage]
            public IRefs<Integration> integrationRefs;
        }

        public const string AuthorizationPropertyName = "authorization";
        [ApiProperty(PropertyName = AuthorizationPropertyName)]
        [JsonProperty(PropertyName = AuthorizationPropertyName)]
        [Storage(Name = AuthorizationPropertyName)]
        public IRefOptional<Authorization> authorization { get; set; }
        
        [Api.HttpPost] //(MatchAllBodyParameters = false)]
        public async static Task<HttpResponseMessage> CreateAsync(
                [Property(Name = AccountPropertyName)]Guid accountId,
                [PropertyOptional(Name = AuthorizationPropertyName)]IRef<Authorization> authorizationRefMaybe,
                [Resource]Integration integration,
                Api.Azure.AzureApplication application, EastFive.Api.Controllers.Security security,
            CreatedResponse onCreated,
            AlreadyExistsResponse onAlreadyExists,
            ForbiddenResponse forbidden,
            ReferencedDocumentDoesNotExistsResponse<Authorization> onAuthenticationDoesNotExist,
            GeneralConflictResponse onFailure)
        {
            if (!await application.CanAdministerCredentialAsync(accountId, security))
                return forbidden();

            return await await authorizationRefMaybe.StorageGetAsync(
                authorization =>
                {
                    // TODO? This
                    // var accountIdDidMatch = await await authorization.ParseCredentailParameters(
                    return CreateWithAuthorization(integration, authorization,
                            accountId,
                        () => onCreated(),
                        () => onAlreadyExists(),
                        (why) => onFailure(why));
                },
                async () =>
                {
                    return await await integration.StorageCreateAsync(
                        discard =>
                        {
                            return SaveAccountLookupAsync(accountId, integration,
                                () => onCreated());
                        },
                        () => onAlreadyExists().AsTask());
                });
        }

        private static async Task<TResult> CreateWithAuthorization<TResult>(
            Integration integration, Authorization authorization,
            Guid accountId,
            Func<TResult> onCreated,
            Func<TResult> onAlreadyExists,
            Func<string, TResult> onFailure)
        {
            integration.Method = authorization.Method; // method is used in the .mappingId
            var authorizationLookup = new AuthorizationIntegrationLookup
            {
                integrationMappingRef = integration.integrationRef,
                authorizationLookupRef = authorization.authorizationRef,
            };
            return await await authorizationLookup.StorageCreateAsync(
                async (idDiscardAuthorizationLookup) =>
                {
                    return await await integration.StorageCreateAsync(
                        discard =>
                        {
                            return SaveAccountLookupAsync(accountId, integration, onCreated);
                        },
                        () => onAlreadyExists().AsTask());
                },
                () =>
                {
                    // TODO: Check if mapping is to this integration and reply already created.
                    return onFailure("Authorization is already mapped to another integration.").AsTask();
                });
        }

        private static async Task<TResult> SaveAccountLookupAsync<TResult>(
                Guid accountId, Integration integration,
            Func<TResult> onCreated)
        {
            var accountLookupRef = new Ref<AccountIntegrationLookup>(accountId);
            return await accountLookupRef.StorageCreateOrUpdateAsync(
                (accountLookup) =>
                {
                    accountLookup.accountIntegrationLookupRef = accountLookupRef;
                    return accountLookup;
                },
                async (created, accountLookup, saveAsync) =>
                {
                    accountLookup.integrationRefs = accountLookup.integrationRefs.IsDefaultOrNull() ?
                        integration.integrationRef.id.AsArray().AsRefs<Integration>()
                        :
                        accountLookup.integrationRefs.ids
                            .Append(integration.integrationRef.id)
                            .AsRefs<Integration>();
                    await saveAsync(accountLookup);
                    return onCreated();
                });
        }

        public static async Task<TResult> CreateByMethodAndKeyAsync<TResult>(IRef<Method> method, 
                Guid accountId, IDictionary<string, string> parameters,
            Func<TResult> onCreated,
            Func<string, TResult> onFailure)
        {
            var authorizationRef = new Ref<Authorization>(Guid.NewGuid());
            var authorization = new Authorization
            {
                authorizationRef = authorizationRef,
                parameters = parameters,
                Method = method,
            };
            return await await authorization.StorageCreateAsync<Authorization, Task<TResult>>(
                (discardId) =>
                {
                    var integration = new Integration
                    {
                        integrationRef = Guid.NewGuid().AsRef<Integration>(),
                        accountId = accountId,
                        authorization = authorizationRef.Optional(),
                        Method = method,
                    };
                    return CreateWithAuthorization(integration, authorization,
                            accountId,
                        () => onCreated(),
                        () => throw new Exception("Guid not unique"),
                        (why) => onFailure(why));
                },
                () => throw new Exception("Guid not unique"));
        }

        public static async Task<TResult> GetParametersByAccountIdAsync<TResult>(IRef<Method> methodId, Guid accountId,
            Func<IEnumerableAsync<KeyValuePair<Integration, Authorization>>, TResult> onFound,
            Func<TResult> onNotFound)
        {
            var accountIntegrationLookupRef = new Ref<AccountIntegrationLookup>(accountId);
            return await accountIntegrationLookupRef.StorageGetAsync(
                accountIntegrationLookup =>
                {
                    var integrationsKvp = accountIntegrationLookup.integrationRefs
                        .StorageGet()
                        .Where(integration => integration.Method.id == methodId.id)
                        .Where(integration => integration.authorization.HasValue)
                        .Select(
                            integration =>
                            {
                                return integration.authorization.StorageGetAsync(
                                    authorization =>
                                    {
                                        return authorization.PairWithKey(integration);
                                    },
                                    () => default(KeyValuePair<Integration, Authorization>?));
                            })
                        .Await()
                        .SelectWhereHasValue();
                    return onFound(integrationsKvp);
                },
                onNotFound);
        }
    }
}
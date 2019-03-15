﻿using System;
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
        Route = "AccountMapping",
        Resource = typeof(AccountMapping),
        ContentType = "x-application/auth-account-mapping",
        ContentTypeVersion = "0.1")]
    [StorageTable]
    public struct AccountMapping : IReferenceable
    {
        [JsonIgnore]
        public Guid id => accountMappingId;

        public const string AccountMappingIdPropertyName = "id";
        [ApiProperty(PropertyName = AccountMappingIdPropertyName)]
        [JsonProperty(PropertyName = AccountMappingIdPropertyName)]
        [Storage]
        public Guid accountMappingId;

        [RowKey]
        [StandardParititionKey]
        [JsonIgnore]
        public IRef<AccountMapping> mappingId
        {
            get
            {
                var composeId = this.Method.id
                    .ComposeGuid(this.accountId);
                return new Ref<AccountMapping>(composeId);
            }
            set
            {
            }
        }

        public const string MethodPropertyName = "method";
        [JsonIgnore]
        [Storage(Name = MethodPropertyName)]
        public IRef<Authentication> Method { get; set; }

        public const string AccountPropertyName = "account";
        [ApiProperty(PropertyName = AccountPropertyName)]
        [JsonProperty(PropertyName = AccountPropertyName)]
        [Storage(Name = AccountPropertyName)]
        public Guid accountId { get; set; }

        [StorageTable]
        public struct AccountMappingLookup : IReferenceable
        {
            [JsonIgnore]
            public Guid id => accountMappingLookupId.id;

            [RowKey]
            [StandardParititionKey]
            [JsonIgnore]
            public IRef<AccountMappingLookup> accountMappingLookupId
            {
                get
                {
                    return GetLookup(this.Method, this.accountkey);
                }
                set
                {
                }
            }

            public const string AccountKeyPropertyName = "account";
            [JsonIgnore]
            [Storage]
            public string accountkey { get; set; }

            public const string MethodPropertyName = "method";
            [JsonIgnore]
            [Storage]
            public IRef<Authentication> Method { get; set; }

            [JsonIgnore]
            [Storage]
            public IRef<AccountMapping> accountMappingId;

            public static IRef<AccountMappingLookup> GetLookup(
                IRef<Authentication> method, string accountkey)
            {
                var composeId = method.id
                    .ComposeGuid(accountkey.MD5HashGuid());
                return new Ref<AccountMappingLookup>(composeId);
            }
        }

        [StorageTable]
        public struct AuthorizationLookup : IReferenceable
        {
            [JsonIgnore]
            public Guid id => authorizationLookupRef.id;

            [RowKey]
            [StandardParititionKey]
            [JsonIgnore]
            public IRef<Authorization> authorizationLookupRef;

            [JsonIgnore]
            [Storage]
            public IRef<AccountMapping> accountMappingRef;
        }

        public const string AuthorizationPropertyName = "authorization";
        [ApiProperty(PropertyName = AuthorizationPropertyName)]
        [JsonProperty(PropertyName = AuthorizationPropertyName)]
        [Storage(Name = AuthorizationPropertyName)]
        public IRef<Authorization> authorization { get; set; }

        [Storage]
        public IRefOptional<AccountMappingLookup> accountMappingLookup { get; set; }

        [Api.HttpPost] //(MatchAllBodyParameters = false)]
        public async static Task<HttpResponseMessage> CreateAsync(
                [Property(Name = AccountPropertyName)]Guid accountId,
                [Property(Name = AuthorizationPropertyName)]IRef<Authorization> authorizationRef,
                [Resource]AccountMapping accountMapping,
                Api.Azure.AzureApplication application,
            CreatedResponse onCreated,
            ForbiddenResponse forbidden,
            ReferencedDocumentDoesNotExistsResponse<Authorization> onAuthenticationDoesNotExist,
            GeneralConflictResponse onFailure)
        {
            return await await authorizationRef.StorageGetAsync(
                async authorization =>
                {
                    accountMapping.Method = authorization.Method;
                    var authorizationLookup = new AuthorizationLookup
                    {
                        accountMappingRef = accountMapping.mappingId,
                        authorizationLookupRef = authorizationRef,
                    };
                    return await await authorizationLookup.StorageCreateAsync(
                        async (idDiscard) =>
                        {
                            accountMapping.Method = authorization.Method;
                            accountMapping.accountMappingLookup = await await authorization.ParseCredentailParameters(
                                    application,
                                (accountKey, loginProvider) =>
                                {
                                    var lookup = new AccountMappingLookup()
                                    {
                                        accountkey = accountKey,
                                        accountMappingId = accountMapping.mappingId,
                                        Method = authorization.Method,
                                    };
                                    return lookup.StorageCreateAsync(
                                        (discard) => new RefOptional<AccountMappingLookup>(
                                            lookup.accountMappingLookupId),
                                        () => new RefOptional<AccountMappingLookup>());
                                },
                                (why) =>
                                {
                                    var amLookupMaybe = new RefOptional<AccountMappingLookup>();
                                    return amLookupMaybe.AsTask();
                                });
                            return await accountMapping.StorageCreateAsync(
                                createdId =>
                                {
                                    return onCreated();
                                },
                                () => forbidden().AddReason("Account is already mapped to that authentication."));
                        },
                        () => onFailure("Authorization is already mapped to another account.").AsTask());
                },
                () => onAuthenticationDoesNotExist().AsTask());
        }

        internal static Task<TResult> CreateByMethodAndKeyAsync<TResult>(IRef<Authentication> method, 
                string externalAccountKey, Guid internalAccountId,
            Func<TResult> onCreated)
        {
            throw new NotImplementedException();
        }

        internal static async Task<TResult> FindByMethodAndKeyAsync<TResult>(IRef<Authentication> authenticationId, string authorizationKey,
                Authorization authorization,
            Func<Guid, TResult> onFound,
            Func<TResult> onNotFound)
        {
            var lookupRef = AccountMappingLookup.GetLookup(authenticationId, authorizationKey);
            return await await lookupRef.StorageGetAsync(
                lookup =>
                {
                    return lookup.accountMappingId.StorageGetAsync(
                        accountMapping => onFound(accountMapping.accountId),
                        () => onNotFound());
                },
                async () =>
                {
                    var accountMappingRef = new Ref<AuthorizationLookup>(authorization.id);
                    return await await accountMappingRef.StorageGetAsync(
                        lookup =>
                        {
                            return lookup.accountMappingRef.StorageGetAsync(
                                accountMapping => onFound(accountMapping.accountId),
                                () => onNotFound());
                        },
                        () => onNotFound().AsTask());
                });
        }
    }
}
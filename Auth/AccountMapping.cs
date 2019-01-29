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
using EastFive.Security;
using EastFive.Serialization;
using Newtonsoft.Json;

namespace EastFive.Azure.Auth
{
    [DataContract]
    [FunctionViewController(
        Route = "AccountMapping",
        Resource = typeof(AccountMapping),
        ContentType = "x-application/auth-account-mapping",
        ContentTypeVersion = "0.1")]
    public struct AccountMapping : IReferenceable
    {
        public Guid id => accountMappingRef.id;

        public const string AccountMappingIdPropertyName = "id";
        [ApiProperty(PropertyName = AccountMappingIdPropertyName)]
        [JsonProperty(PropertyName = AccountMappingIdPropertyName)]
        [StorageProperty(Name = AccountMappingIdPropertyName)]
        public IRef<Authorization> accountMappingRef;
        
        public const string AccountPropertyName = "account";
        [ApiProperty(PropertyName = AccountPropertyName)]
        [JsonProperty(PropertyName = AccountPropertyName)]
        [StorageProperty(Name = AccountPropertyName)]
        public Guid account { get; set; }

        public const string MethodPropertyName = "method";
        [ApiProperty(PropertyName = MethodPropertyName)]
        [JsonProperty(PropertyName = MethodPropertyName)]
        [StorageProperty(Name = MethodPropertyName)]
        public IRef<Authentication> Method { get; set; }

        public const string AuthorizationPropertyName = "authorization";
        [ApiProperty(PropertyName = AuthorizationPropertyName)]
        [JsonProperty(PropertyName = AuthorizationPropertyName)]
        [StorageProperty(Name = AuthorizationPropertyName)]
        public IRef<Authorization> authorization { get; set; }

        [StorageProperty(IsRowKey =true, Name = "AccountMethodLookup")]
        public Guid accountMethodLookup
        {
            get
            {
                return account.ComposeGuid(Method.id);
            }
            set
            {

            }
        }

        public const string ParametersPropertyName = "parameters";
        [StorageProperty(Name = ParametersPropertyName)]
        public Dictionary<string, string> parameters;

        [Api.HttpPost] //(MatchAllBodyParameters = false)]
        public async static Task<HttpResponseMessage> CreateAsync(
                [Property(Name = AccountMappingIdPropertyName)]Guid accountMappingId,
                [Property(Name = AccountPropertyName)]Guid accountId,
                [Property(Name = MethodPropertyName)]IRef<Authentication> methodRef,
                [Property(Name = AuthorizationPropertyName)]IRef<Authorization> authorizationRef,
                [Resource]AccountMapping accountMapping,
                Api.Azure.AzureApplication application, UrlHelper urlHelper,
            CreatedResponse onCreated,
            ForbiddenResponse forbidden,
            ReferencedDocumentDoesNotExistsResponse<Authentication> onAuthenticationDoesNotExist)
        {
            return await await Authentication.ById(methodRef, application, urlHelper,
                async (authentication) =>
                {
                    await authorizationRef.ResolveAsync();
                    if (!authorizationRef.value.HasValue)
                        return onAuthenticationDoesNotExist();

                    var authorization = authorizationRef.value.Value;
                    return await accountMapping.StorageCreateAsync(
                        createdId => onCreated(),
                        () => forbidden().AddReason("Account is already mapped to that authentication."));
                },
                () => onAuthenticationDoesNotExist().AsTask());
        }
    }
}
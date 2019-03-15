//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Linq.Expressions;
//using System.Net.Http;
//using System.Runtime.Serialization;
//using System.Threading.Tasks;
//using System.Web.Http.Routing;
//using BlackBarLabs.Api;
//using EastFive.Api;
//using EastFive.Api.Controllers;
//using EastFive.Azure.Persistence.AzureStorageTables;
//using EastFive.Collections.Generic;
//using EastFive.Extensions;
//using EastFive.Linq;
//using EastFive.Linq.Async;
//using EastFive.Persistence;
//using EastFive.Security;
//using EastFive.Serialization;
//using Newtonsoft.Json;

//namespace EastFive.Azure.Auth
//{
//    [DataContract]
//    [FunctionViewController(
//        Route = "AuthenticationAccountMapping",
//        Resource = typeof(AuthenticationAccountMapping),
//        ContentType = "x-application/auth-AuthenticationAccountMapping",
//        ContentTypeVersion = "0.1")]
//    public struct AuthenticationAccountMapping : IReferenceable
//    {
//        public Guid id => AuthenticationAccountMappingRef.id;

//        public const string AuthenticationAccountMappingIdPropertyName = "id";
//        [ApiProperty(PropertyName = AuthenticationAccountMappingIdPropertyName)]
//        [JsonProperty(PropertyName = AuthenticationAccountMappingIdPropertyName)]
//        [StorageProperty(Name = AuthenticationAccountMappingIdPropertyName)]
//        public IRef<AuthenticationAccountMapping> AuthenticationAccountMappingRef;
        
//        public const string MethodPropertyName = "method";
//        [StorageProperty(Name = MethodPropertyName)]
//        public IRef<Authentication> method { get; set; }

//        [StorageProperty(IsRowKey = true, Name = "AccountMethodLookup")]
//        public Guid accountMethodLookup
//        {
//            get
//            {
//                return account.ComposeGuid(method.id);
//            }
//            set
//            {

//            }
//        }

//        public const string AccountPropertyName = "account";
//        [ApiProperty(PropertyName = AccountPropertyName)]
//        [JsonProperty(PropertyName = AccountPropertyName)]
//        [StorageProperty(Name = AccountPropertyName)]
//        public Guid account { get; set; }

//        public const string AuthorizationPropertyName = "authorization";
//        [ApiProperty(PropertyName = AuthorizationPropertyName)]
//        [JsonProperty(PropertyName = AuthorizationPropertyName)]
//        [StorageProperty(Name = AuthorizationPropertyName)]
//        public IRefOptional<Authorization> authorization { get; set; }

//        public async Task<TResult> GetAuthenticationKeyAsync<TResult>(
//                Api.Azure.AzureApplication application,
//            Func<string, TResult> onSuccess,
//            Func<TResult> onNotFound)
//        {
//            if (!authorization.HasValue)
//                return onNotFound();

//            var gettingMethod = method.ResolveAsync();

//            var authorizationRef = authorization.Ref;
//            await authorizationRef.ResolveAsync();
//            var authorizationMaybe = authorizationRef.value;
//            if (!authorizationMaybe.HasValue)
//                return onNotFound();
//            var authorizationInst = authorizationMaybe.Value;

//            await gettingMethod;
//            var methodMaybe = method.value;
//            if (!methodMaybe.HasValue)
//                return onNotFound();
//            var methodInst = methodMaybe.Value;

//            var authenticationKey = await methodInst.GetAuthorizationKeyAsync(application, authorizationInst.parameters);
//            return onSuccess(authenticationKey);
//        }


//        [Api.HttpPost]
//        public async static Task<HttpResponseMessage> CreateAsync(
//                [Property(Name = AuthenticationAccountMappingIdPropertyName)]Guid accountMappingId,
//                [Property(Name = AccountPropertyName)]Guid accountId,
//                [Property(Name = AuthorizationPropertyName)]IRef<Authorization> authorizationRef,
//                [Resource]AccountMapping accountMapping,
//                Api.Azure.AzureApplication application, UrlHelper urlHelper,
//            CreatedResponse onCreated,
//            ForbiddenResponse forbidden,
//            ReferencedDocumentDoesNotExistsResponse<Authentication> onAuthenticationDoesNotExist)
//        {
//            await authorizationRef.ResolveAsync();
//            if (!authorizationRef.value.HasValue)
//                return onAuthenticationDoesNotExist();

//            var authorization = authorizationRef.value.Value;
//            accountMapping.Method = authorization.Method;
//            return await accountMapping.StorageCreateAsync(
//                createdId => onCreated(),
//                () => forbidden().AddReason("Account is already mapped to that authentication."));
//        }
//    }
//}
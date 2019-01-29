using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http.Routing;
using EastFive.Api;
using EastFive.Api.Controllers;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Persistence;
using EastFive.Security;
using Newtonsoft.Json;

namespace EastFive.Azure.Auth
{
    [DataContract]
    [FunctionViewController(
        Route = "XAuthorization",
        Resource = typeof(Authorization),
        ContentType = "x-application/auth-authorization",
        ContentTypeVersion = "0.1")]
    public struct Authorization : IReferenceable
    {
        public Guid id => authorizationId.id;

        public const string AuthorizationIdPropertyName = "id";
        [ApiProperty(PropertyName = AuthorizationIdPropertyName)]
        [JsonProperty(PropertyName = AuthorizationIdPropertyName)]
        [StorageProperty(IsRowKey = true, Name = AuthorizationIdPropertyName)]
        public IRef<Authorization> authorizationId;
        
        public const string MethodPropertyName = "method";
        [ApiProperty(PropertyName = MethodPropertyName)]
        [JsonProperty(PropertyName = MethodPropertyName)]
        [StorageProperty(Name = MethodPropertyName)]
        public IRef<Authentication> Method { get; set; }

        public const string LocationLogoutPropertyName = "location_logout";
        [ApiProperty(PropertyName = LocationLogoutPropertyName)]
        [JsonProperty(PropertyName = LocationLogoutPropertyName)]
        [StorageProperty(Name = LocationLogoutPropertyName)]
        public Uri LocationLogout { get; set; }

        public const string LocationLogoutReturnPropertyName = "location_logout_return";
        [ApiProperty(PropertyName = LocationLogoutReturnPropertyName)]
        [JsonProperty(PropertyName = LocationLogoutReturnPropertyName)]
        [StorageProperty(Name = LocationLogoutReturnPropertyName)]
        public Uri LocationLogoutReturn { get; set; }

        public const string LocationAuthorizationPropertyName = "location_authentication";
        [ApiProperty(PropertyName = LocationAuthorizationPropertyName)]
        [JsonProperty(PropertyName = LocationAuthorizationPropertyName)]
        [StorageProperty(Name = LocationAuthorizationPropertyName)]
        public Uri LocationAuthentication { get; set; }

        public const string LocationAuthorizationReturnPropertyName = "location_authentication_return";
        [ApiProperty(PropertyName = LocationAuthorizationReturnPropertyName)]
        [JsonProperty(PropertyName = LocationAuthorizationReturnPropertyName)]
        [StorageProperty(Name = LocationAuthorizationReturnPropertyName)]
        public Uri LocationAuthenticationReturn { get; set; }

        public const string ParametersPropertyName = "parameters";
        [StorageProperty(Name = ParametersPropertyName)]
        public Dictionary<string, string> parameters;

        [Api.HttpPost] //(MatchAllBodyParameters = false)]
        public async static Task<HttpResponseMessage> CreateAsync(
                [Property(Name = AuthorizationIdPropertyName)]Guid authorizationId,
                [Property(Name = MethodPropertyName)]IRef<Authentication> method,
                [Property(Name = LocationAuthorizationReturnPropertyName)]Uri LocationAuthenticationReturn,
                [Resource]Authorization authorization,
                Api.Azure.AzureApplication application, UrlHelper urlHelper,
            CreatedBodyResponse<Authorization> onCreated,
            ForbiddenResponse forbidden,
            ReferencedDocumentDoesNotExistsResponse<Authentication> onAuthenticationDoesNotExist)
        {
            return await await Authentication.ById(method, application, urlHelper,
                async (authentication) =>
                {
                    var authorizationIdSecure = SecureGuid.Generate();
                    authorization.authorizationId = new Ref<Authorization>(authorizationIdSecure);
                    authorization.LocationAuthentication = await authentication.GetLoginUrlAsync(
                        application, authorizationIdSecure, authorization.LocationAuthenticationReturn);

                    return await authorization.StorageCreateAsync(
                        createdId => onCreated(authorization),
                        () => throw new Exception("Secure Guid not unique"));
                },
                () => onAuthenticationDoesNotExist().AsTask());
        }
    }
}
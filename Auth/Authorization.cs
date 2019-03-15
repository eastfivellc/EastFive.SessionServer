using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http.Routing;
using EastFive.Api;
using EastFive.Api.Azure;
using EastFive.Api.Controllers;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;
using EastFive.Security;
using EastFive.Security.SessionServer;
using EastFive.Security.SessionServer.Exceptions;
using Microsoft.ApplicationInsights;
using Newtonsoft.Json;

namespace EastFive.Azure.Auth
{
    [DataContract]
    [FunctionViewController4(
        Route = "XAuthorization",
        Resource = typeof(Authorization),
        ContentType = "x-application/auth-authorization",
        ContentTypeVersion = "0.1")]
    [StorageTable]
    public struct Authorization : IReferenceable
    {
        public Guid id => authorizationId.id;

        public const string AuthorizationIdPropertyName = "id";
        [ApiProperty(PropertyName = AuthorizationIdPropertyName)]
        [JsonProperty(PropertyName = AuthorizationIdPropertyName)]
        [RowKey]
        [StandardParititionKey]
        public IRef<Authorization> authorizationId;
        
        public const string MethodPropertyName = "method";
        [ApiProperty(PropertyName = MethodPropertyName)]
        [JsonProperty(PropertyName = MethodPropertyName)]
        [Storage(Name = MethodPropertyName)]
        public IRef<Authentication> Method { get; set; }

        public const string LocationLogoutPropertyName = "location_logout";
        [ApiProperty(PropertyName = LocationLogoutPropertyName)]
        [JsonProperty(PropertyName = LocationLogoutPropertyName)]
        [Storage(Name = LocationLogoutPropertyName)]
        public Uri LocationLogout { get; set; }

        public const string LocationLogoutReturnPropertyName = "location_logout_return";
        [ApiProperty(PropertyName = LocationLogoutReturnPropertyName)]
        [JsonProperty(PropertyName = LocationLogoutReturnPropertyName)]
        [Storage(Name = LocationLogoutReturnPropertyName)]
        public Uri LocationLogoutReturn { get; set; }

        public const string LocationAuthorizationPropertyName = "location_authentication";
        [ApiProperty(PropertyName = LocationAuthorizationPropertyName)]
        [JsonProperty(PropertyName = LocationAuthorizationPropertyName)]
        [Storage(Name = LocationAuthorizationPropertyName)]
        public Uri LocationAuthentication { get; set; }

        public const string LocationAuthorizationReturnPropertyName = "location_authentication_return";
        [ApiProperty(PropertyName = LocationAuthorizationReturnPropertyName)]
        [JsonProperty(PropertyName = LocationAuthorizationReturnPropertyName)]
        [Storage(Name = LocationAuthorizationReturnPropertyName)]
        public Uri LocationAuthenticationReturn { get; set; }

        public const string ParametersPropertyName = "parameters";
        [JsonIgnore]
        [Storage(Name = ParametersPropertyName)]
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
            return await await Authentication.ById(method, application,
                async (authentication) =>
                {
                    var authorizationIdSecure = authentication.authenticationId;
                    authorization.LocationAuthentication = await authentication.GetLoginUrlAsync(
                        application, urlHelper, authorizationIdSecure.id, authorization.LocationAuthenticationReturn);

                    return await authorization.StorageCreateAsync(
                        createdId => onCreated(authorization),
                        () => throw new Exception("Secure Guid not unique"));
                },
                () => onAuthenticationDoesNotExist().AsTask());
        }

        public async Task<TResult> ParseCredentailParameters<TResult>(
                Api.Azure.AzureApplication application,
            Func<string, IProvideLogin, TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            var parameters = this.parameters;
            return await await Authentication.ById(this.Method, application, // TODO: Cleanup 
                (method) =>
                {
                    return application.LoginProviders
                        .SelectValues()
                        .Where(loginProvider => loginProvider.Method == method.name)
                        .FirstAsync(
                            (loginProvider) =>
                            {
                                return loginProvider.ParseCredentailParameters(parameters,
                                    (userKey, authorizationId, deprecatedId) =>
                                    {
                                        return onSuccess(userKey, loginProvider);
                                    },
                                    (why) => onFailure(why));
                            },
                            () => onFailure("Method does not match any existing authentication."));
                },
                () => onFailure("Authentication not found").AsTask());
        }
    }
}
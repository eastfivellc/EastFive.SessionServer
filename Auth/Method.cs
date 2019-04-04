using EastFive.Api;
using EastFive.Persistence;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Web.Http.Routing;
using EastFive.Linq.Async;
using EastFive.Api.Controllers;
using System.Runtime.Serialization;
using EastFive.Extensions;
using EastFive.Collections.Generic;
using EastFive.Persistence.Azure.StorageTables;

namespace EastFive.Azure.Auth
{
    [DataContract]
    [FunctionViewController4(
        Route = "AuthenticationMethod",
        Resource = typeof(Method),
        ContentType = "x-application/auth-authentication-method",
        ContentTypeVersion = "0.1")]
    public struct Method
    {
        public Guid id => authenticationId.id;

        public const string AuthenticationIdPropertyName = "id";
        [ApiProperty(PropertyName = AuthenticationIdPropertyName)]
        [JsonProperty(PropertyName = AuthenticationIdPropertyName)]
        [RowKey]
        [StandardParititionKey]
        public IRef<Method> authenticationId;

        public const string NamePropertyName = "name";
        [ApiProperty(PropertyName = NamePropertyName)]
        [JsonProperty(PropertyName = NamePropertyName)]
        [Storage(Name = NamePropertyName)]
        public string name;

        private Task<TResult> GetLoginProviderAsync<TResult>(Api.Azure.AzureApplication application,
            Func<string, Security.SessionServer.IProvideLogin, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return GetLoginProviderAsync(this.id, application,
                onFound,
                onNotFound);
        }

        private static Task<TResult> GetLoginProviderAsync<TResult>(Guid authenticationId, Api.Azure.AzureApplication application,
            Func<string, Security.SessionServer.IProvideLogin, TResult> onFound,
            Func<TResult> onNotFound)
        {
            //var debug = application.LoginProviders.ToArrayAsync().Result;
            return application.LoginProviders
                .Where(
                    loginProvider =>
                    {
                        return loginProvider.Value.Id == authenticationId;
                    })
                .FirstAsync(
                    (loginProviderKvp) =>
                    {
                        var loginProviderKey = loginProviderKvp.Key;
                        var loginProvider = loginProviderKvp.Value;
                        return onFound(loginProviderKey, loginProvider);
                    },
                    onNotFound);
        }

        [HttpGet]
        public static Task<HttpResponseMessage> QueryAsync(
            Api.Azure.AzureApplication application,
            MultipartResponseAsync<Method> onContent)
        {
            return onContent(
                application.LoginProviders
                    .Select(
                        (loginProvider) =>
                        {
                            return new Method
                            {
                                authenticationId = new Ref<Method>(loginProvider.Value.Id),
                                name = loginProvider.Value.Method,
                            };
                        }));
        }

        [HttpGet]
        public static Task<HttpResponseMessage> QueryByIntegrationAsync(
            [QueryParameter(Name = "integration")]IRef<Integration> integrationRef,
            Api.Azure.AzureApplication application,
            MultipartResponseAsync<Method> onContent)
        {
            var integrationProviders = application.LoginProviders
                .Where(loginProvider => loginProvider.GetType().IsSubClassOfGeneric(typeof(IProvideIntegration)))
                .Select(
                    (loginProvider) =>
                    {
                        return new Method
                        {
                            authenticationId = new Ref<Method>(loginProvider.Value.Id),
                            name = loginProvider.Value.Method,
                        };
                    });
            return onContent(integrationProviders);
        }

        internal static Task<TResult> ById<TResult>(IRef<Method> method, Api.Azure.AzureApplication application,
            Func<Method, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return GetLoginProviderAsync(method.id, application,
                (key, loginProvider) =>
                {
                    var authentication = new Method
                    {
                        authenticationId = new Ref<Method>(loginProvider.Id),
                        name = loginProvider.Method,
                    };
                    return onFound(authentication);
                },
                onNotFound);
        }

        public static Task<Method> ByMethodName(string methodName, Api.Azure.AzureApplication application)
        {
            return application.LoginProviders
                .SelectValues()
                .Where(loginProvider => loginProvider.Method == methodName)
                .FirstAsync(
                    (loginProvider) =>
                    {
                        return new Method
                        {
                            authenticationId = new Ref<Method>(loginProvider.Id),
                            name = loginProvider.Method,
                        };
                    },
                    () => throw new Exception($"Login provider `{methodName}` is not enabled."));
        }

        public async Task<TResult> RedeemTokenAsync<TResult>(
                IDictionary<string, string> parameters,
                Api.Azure.AzureApplication application,
            Func<string, IRefOptional<Authorization>, Security.SessionServer.IProvideLogin, IDictionary<string, string>, TResult> onSuccess,
            Func<Guid?, IDictionary<string, string>, TResult> onLogout,
            Func<string, TResult> onFailure)
        {
            var methodName = this.name;
            var matchingLoginProviders = await application.LoginProviders
                .SelectValues()
                .Where(loginProvider => loginProvider.Method == methodName)
                .ToArrayAsync();
            if (!matchingLoginProviders.Any())
                return onFailure("Method does not match any existing authentication.");
            var matchingLoginProvider = matchingLoginProviders.First();

            return await matchingLoginProvider.RedeemTokenAsync(parameters,
                (userKey, authorizationIdMaybe, deprecatedId, updatedParameters) =>
                {
                    var allParameters = updatedParameters
                        .Concat(
                            parameters
                                .Where(param => !updatedParameters.ContainsKey(param.Key)))
                        .ToDictionary();
                    var authorizationRef = authorizationIdMaybe.HasValue ?
                        new RefOptional<Authorization>(authorizationIdMaybe.Value)
                        :
                        new RefOptional<Authorization>();
                    return onSuccess(userKey, authorizationRef,
                        matchingLoginProvider, allParameters);
                },
                (authorizationId, extraParams) => onLogout(authorizationId, extraParams),
                (why) => onFailure(why),
                (why) => onFailure(why),
                (why) => onFailure(why),
                (why) => onFailure(why));
        }

        internal Task<Uri> GetLoginUrlAsync(Api.Azure.AzureApplication application, UrlHelper urlHelper, Guid authorizationIdSecure, Uri responseLocation)
        {
            var authenticationId = this.id;
            return GetLoginProviderAsync(application,
                (name, loginProvider) => loginProvider.GetLoginUrl(authorizationIdSecure, responseLocation,
                    type => urlHelper.GetLocation(type)),
                () => throw new Exception($"Login provider with id {authenticationId} does not exists."));
        }

        public Task<string> GetAuthorizationKeyAsync(Api.Azure.AzureApplication application, IDictionary<string, string> parameters)
        {
            var authenticationId = this.id;
            return GetLoginProviderAsync(application,
                (name, loginProvider) => loginProvider.ParseCredentailParameters(parameters,
                    (externalUserKey, authenticationIdMaybe, scopeMaybeDiscard) => externalUserKey,
                    why => throw new Exception(why)),
                () => throw new Exception($"Login provider with id {authenticationId} does not exists."));
        }
    }
}

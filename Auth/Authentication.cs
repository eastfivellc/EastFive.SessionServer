﻿using EastFive.Api;
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

namespace EastFive.Azure.Auth
{
    [DataContract]
    [FunctionViewController(
        Route = "Authentication",
        Resource = typeof(Authentication),
        ContentType = "x-application/auth-authentication",
        ContentTypeVersion = "0.1")]
    public struct Authentication
    {
        public Guid id => authenticationId.id;

        public const string AuthenticationIdPropertyName = "id";
        [ApiProperty(PropertyName = AuthenticationIdPropertyName)]
        [JsonProperty(PropertyName = AuthenticationIdPropertyName)]
        [StorageProperty(IsRowKey = true, Name = AuthenticationIdPropertyName)]
        public IRef<Authentication> authenticationId;

        public const string NamePropertyName = "name";
        [ApiProperty(PropertyName = NamePropertyName)]
        [JsonProperty(PropertyName = NamePropertyName)]
        [StorageProperty(Name = NamePropertyName)]
        public string name;

        private UrlHelper urlHelper;

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
            return application.LoginProviders
                .Where(loginProvider => loginProvider.Value.Id == authenticationId)
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
            MultipartResponseAsync<Authentication> onContent)
        {
            return onContent(
                application.LoginProviders
                    .Select(
                        (loginProvider) =>
                        {
                            return new Authentication
                            {
                                authenticationId = new Ref<Authentication>(loginProvider.Value.Id),
                                name = loginProvider.Value.Method,
                            };
                        }));
        }

        [HttpGet]
        public static Task<HttpResponseMessage> QueryByIntegrationAsync(
            [QueryParameter(Name = "integration")]IRef<Integration> integrationRef,
            Api.Azure.AzureApplication application,
            MultipartResponseAsync<Authentication> onContent)
        {
            var integrationProviders = application.LoginProviders
                .Where(loginProvider => loginProvider.GetType().IsSubClassOfGeneric(typeof(IProvideIntegration)))
                .Select(
                    (loginProvider) =>
                    {
                        return new Authentication
                        {
                            authenticationId = new Ref<Authentication>(loginProvider.Value.Id),
                            name = loginProvider.Value.Method,
                        };
                    });
            return onContent(integrationProviders);
        }

        internal static Task<TResult> ById<TResult>(IRef<Authentication> method, Api.Azure.AzureApplication application, UrlHelper urlHelper,
            Func<Authentication, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return GetLoginProviderAsync(method.id, application,
                (key, loginProvider) =>
                {
                    var authentication = new Authentication
                    {
                        authenticationId = new Ref<Authentication>(loginProvider.Id),
                        name = loginProvider.Method,
                        urlHelper = urlHelper,
                    };
                    return onFound(authentication);
                },
                onNotFound);
        }

        internal Task<Uri> GetLoginUrlAsync(Api.Azure.AzureApplication application, Guid authorizationIdSecure, Uri responseLocation)
        {
            var urlHelper = this.urlHelper;
            var authenticationId = this.id;
            return GetLoginProviderAsync(application,
                (name, loginProvider) => loginProvider.GetLoginUrl(authorizationIdSecure, responseLocation,
                    type => urlHelper.GetLocation(type)),
                () => throw new Exception($"Login provider with id {authenticationId} does not exists."));
        }

        public Task<string> GetAuthorizationKeyAsync(Api.Azure.AzureApplication application, Dictionary<string, string> parameters)
        {
            var authenticationId = this.id;
            return GetLoginProviderAsync(application,
                (name, loginProvider) => loginProvider.ParseCredentailParameters(parameters,
                    (authorizationKey, authenticationIdMaybe, scopeMaybeDiscard) => authorizationKey,
                    why => throw new Exception(why)),
                () => throw new Exception($"Login provider with id {authenticationId} does not exists."));
        }
    }
}
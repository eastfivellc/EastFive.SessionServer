using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive.Api;
using EastFive.Api.Controllers;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Extensions;
using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;
using EastFive.Security;
using EastFive.Serialization;
using System.Web.Http.Routing;

namespace EastFive.Azure.Login
{
    [FunctionViewController4(
        Route = "Authentication",
        Resource = typeof(Authentication),
        ContentType = "x-application/login-authentication",
        ContentTypeVersion = "0.1")]
    [StorageTable]
    [Html(Title = "Login")]
    public struct Authentication : IReferenceable
    {
        [JsonIgnore]
        public Guid id => authenticationRef.id;

        public const string AuthenticationPropertyName = "id";
        [ApiProperty(PropertyName = AuthenticationPropertyName)]
        [JsonProperty(PropertyName = AuthenticationPropertyName)]
        [RowKey]
        [StandardParititionKey]
        [HtmlInputHidden]
        public IRef<Authentication> authenticationRef;

        public const string UserIdentificationPropertyName = "user_identification";
        [ApiProperty(PropertyName = UserIdentificationPropertyName)]
        [JsonProperty(PropertyName = UserIdentificationPropertyName)]
        [HtmlInput(Label = "Username or email")]
        public string userIdentification;

        public const string PasswordPropertyName = "password";
        [ApiProperty(PropertyName = PasswordPropertyName)]
        [JsonProperty(PropertyName = PasswordPropertyName)]
        [HtmlInput(Label = "Password")]
        public string password;

        #region Validation of authorization paramters

        public const string AuthenticatedPropertyName = "authenticated";
        [ApiProperty(PropertyName = AuthenticatedPropertyName)]
        [JsonProperty(PropertyName = AuthenticatedPropertyName)]
        [Storage]
        public DateTime? authenticated;

        public const string StatePropertyName = "state";
        [ApiProperty(PropertyName = StatePropertyName)]
        [JsonProperty(PropertyName = StatePropertyName)]
        [Storage]
        public string state;

        public const string ClientPropertyName = "client";
        [ApiProperty(PropertyName = ClientPropertyName)]
        [JsonProperty(PropertyName = ClientPropertyName)]
        [Storage]
        public Guid client;

        public const string ValidationPropertyName = "validation";
        [ApiProperty(PropertyName = ValidationPropertyName)]
        [JsonProperty(PropertyName = ValidationPropertyName)]
        [Storage] // for auditing...?
        public string validation;

        #endregion

        public const string AccountPropertyName = "account";
        [HtmlLink(Label = "Create new account")]
        public IRefOptional<Account> account;


        [Api.HttpGet]
        public static async Task<HttpResponseMessage> GetAsync(
                [QueryParameter(Name = StatePropertyName)]string state,
                [QueryParameter(Name = ClientPropertyName)]IRef<Client> clientRef,
                [QueryParameter(Name = ValidationPropertyName)]string validation,
                Api.Azure.AzureApplication application, UrlHelper urlHelper,
            //ContentTypeResponse<Authentication> onFound,
            RedirectResponse onFound,
            ReferencedDocumentNotFoundResponse<Client> onInvalidClient)
        {
            return await await clientRef.StorageGetAsync(
                (client) =>
                {
                    var authentication = new Authentication
                    {
                        authenticationRef = SecureGuid.Generate().AsRef<Authentication>(),
                        state = state,
                    };
                    return authentication.StorageCreateAsync(
                        (entity) =>
                        {
                            var location = urlHelper.GetLocation<Authentication>(
                                auth => auth.authenticationRef.AssignQueryValue(authentication.authenticationRef),
                                application);
                            return onFound(location, "Found");
                        },
                        () => throw new Exception("Secure guid not unique"));
                },
                () => onInvalidClient().AsTask());
        }

        [Api.HttpPatch]
        [HtmlAction(Label = "Login")]
        public static async Task<HttpResponseMessage> UpdateAsync(
                [UpdateId(Name = AuthenticationPropertyName)]IRef<Authentication> authenticationRef,
                [QueryParameter(Name = UserIdentificationPropertyName)]string userIdentification,
                [QueryParameter(Name = PasswordPropertyName)]string password,
                Api.Azure.AzureApplication application,
                IBuildUrls urlHelper,
            NoContentResponse onUpdated,
            NotFoundResponse onNotFound,
            GeneralConflictResponse onInvalidPassword)
        {
            urlHelper.Resources<Authentication>()
                .Where(
                    b => 
                        b.id == authenticationRef.id &&
                        Authentication.PasswordPropertyName == "asdf");

            return await await authenticationRef.StorageUpdateAsync(
                (authentication, saveAsync) =>
                {
                    return userIdentification
                        .MD5HashGuid()
                        .AsRef<Account>()
                        .StorageGetAsync(
                            async account =>
                            {
                                var passwordHash = Account.GeneratePasswordHash(userIdentification, password);
                                if (passwordHash != account.password)
                                    return onInvalidPassword("Incorrect username or password.");
                                authentication.userIdentification = userIdentification;
                                authentication.authenticated = DateTime.UtcNow;
                                await saveAsync(authentication);
                                return onUpdated();
                            },
                            () => onInvalidPassword("Incorrect username or password.").AsTask());
                },
                () => onNotFound().AsTask());
        }
    }
}

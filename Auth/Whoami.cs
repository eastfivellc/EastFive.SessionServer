using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http.Routing;
using BlackBarLabs.Api.Resources;
using BlackBarLabs.Extensions;
using EastFive.Api;
using EastFive.Api.Controllers;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;
using Newtonsoft.Json;

namespace EastFive.Azure.Auth
{
    [DataContract]
    [FunctionViewController6(
        Route = "Whoami",
        Resource = typeof(Whoami),
        ContentType = "x-application/auth-whoami",
        ContentTypeVersion = "0.1")]
    public struct Whoami
    {
        public const string SessionPropertyName = "session";
        [ApiProperty(PropertyName = SessionPropertyName)]
        [JsonProperty(PropertyName = SessionPropertyName)]
        public IRef<Session> session;

        public const string NamePropertyName = "name";
        [ApiProperty(PropertyName = NamePropertyName)]
        [JsonProperty(PropertyName = NamePropertyName)]
        public string name { get; set; }

        public const string AccountPropertyName = "account";
        [JsonProperty(PropertyName = AccountPropertyName)]
        [ApiProperty(PropertyName = AccountPropertyName)]
        public Guid? account { get; set; }

        [Api.HttpGet] //(MatchAllBodyParameters = false)]
        public static HttpResponseMessage GetAsync(
                EastFive.Api.Controllers.SessionToken security,
                Api.Azure.AzureApplication application, UrlHelper urlHelper,
            ContentTypeResponse<Whoami> onFound)
        {
            var whoami = new Whoami()
            {
                session = security.sessionId.AsRef<Session>(),
                account = security.accountIdMaybe,
            };
            return onFound(whoami);
        }
    }
}
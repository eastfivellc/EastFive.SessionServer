using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Api.Azure.Credentials.Resources.Queries
{
    [DataContract]
    public class ActAsUserQuery : BlackBarLabs.Api.ResourceQueryBase
    {
        [JsonProperty("actorId")]
        public WebIdQuery ActorId { get; set; }

        /// <summary>
        /// Super admin token
        /// </summary>
        [JsonProperty("token")]
        public StringQuery Token { get; set; }

        [JsonProperty("redirectUri")]
        public StringQuery RedirectUri { get; set; }
    }
}

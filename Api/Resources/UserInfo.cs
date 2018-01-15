using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class UserInfo : BlackBarLabs.Api.ResourceBase
    {
        [DataMember]
        [JsonProperty("actor_id")]
        public WebId ActorId { get; set; }
        
        [DataMember]
        [JsonProperty("user_id")]
        public string UserId { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "link")]
        public string Link { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "account_enabled")]
        public bool AccountEnabled { get; set; }
    }
}

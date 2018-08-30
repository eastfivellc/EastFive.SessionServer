using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Api.Azure.Credentials.Resources
{
    [DataContract]
    public class UserInfo : BlackBarLabs.Api.ResourceBase
    {
        [DataMember]
        [JsonProperty("actor_id")]
        public WebId ActorId { get; set; }
        
        [DataMember]
        [JsonProperty("username")]
        public string Username { get; set; }

        [DataMember]
        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "link")]
        public string Link { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "account_enabled")]
        public bool AccountEnabled { get; set; }
    }
}

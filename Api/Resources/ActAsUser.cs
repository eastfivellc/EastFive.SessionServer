using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class ActAsUser : BlackBarLabs.Api.ResourceBase
    {
        #region Properties
        
        [DataMember]
        [JsonProperty("actor_id")]
        public WebId ActorId { get; set; }
        
        [DataMember]
        [JsonProperty("token")]
        public string Token { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "redirect_uri")]
        public string redirect_uri { get; set; }

        #endregion
    }
}

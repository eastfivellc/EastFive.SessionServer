using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class AuthenticationRequestLink : BlackBarLabs.Api.ResourceBase
    {
        #region Properties
        
        [DataMember]
        [JsonProperty(PropertyName = "session_id")]
        public Guid SessionId { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "method")]
        public CredentialValidationMethodTypes Method { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "link")]
        public Uri Link { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "image")]
        public Uri Image { get; set; }

        #endregion
    }
}

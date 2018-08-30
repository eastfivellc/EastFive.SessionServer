using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Api.Azure.Credentials.Api.Resources
{
    [DataContract]
    public class Credential : BlackBarLabs.Api.ResourceBase
    {
        [JsonProperty(PropertyName = "authentication")]
        public WebId Authentication { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "method")]
        public CredentialValidationMethodTypes Method { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "subject")]
        public string Subject { get; set; }
    }
}

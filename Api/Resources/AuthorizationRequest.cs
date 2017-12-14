using BlackBarLabs.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class AuthorizationRequest : BlackBarLabs.Api.ResourceBase
    {
        [DataMember]
        [JsonProperty(PropertyName = "method")]
        public CredentialValidationMethodTypes Method { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "location_authentication")]
        public Uri LocationAuthentication { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "location_authentication_return")]
        public Uri LocationAuthenticationReturn { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "response_token")]
        public Dictionary<string, string> ResponseToken { get; set; }
        
        [DataMember]
        [JsonProperty(PropertyName = "extra_params")]
        public IDictionary<string, string> ExtraParams { get; set; }
        
    }
}
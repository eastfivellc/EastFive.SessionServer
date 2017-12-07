using BlackBarLabs.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class Session : BlackBarLabs.Api.ResourceBase
    {
        [DataMember]
        [JsonProperty(PropertyName = "method")]
        public CredentialValidationMethodTypes Method { get; set; }
        
        [DataMember]
        [JsonProperty(PropertyName = "redirect")]
        public Uri Redirect { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "login")]
        public Uri Login { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "authorization_id")]
        public Guid? AuthorizationId { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "session_header")]
        public string SessionHeader { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "jwt_token")]
        public string JwtToken { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "refresh_token")]
        public string RefreshToken { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "response_token")]
        public Dictionary<string, string> ResponseToken { get; set; }
        
        [DataMember]
        [JsonProperty(PropertyName = "extra_params")]
        public IDictionary<string, string> ExtraParams { get; set; }
        
    }
}
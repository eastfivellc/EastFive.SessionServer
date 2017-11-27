using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class AuthenticationRequest : BlackBarLabs.Api.ResourceBase
    {
        [DataMember]
        [JsonProperty(PropertyName = "method")]
        public CredentialValidationMethodTypes Method { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "action")]
        public AuthenticationActions Action { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "redirect")]
        public Uri Redirect { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "login")]
        public Uri Login { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "session_id")]
        public Guid? SessionId { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "authorization_id")]
        public Guid? AuthorizationId { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "jwt_token")]
        public string JwtToken { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "refresh_token")]
        public string RefreshToken { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "extra_params")]
        public IDictionary<string, string> ExtraParams { get; set; }
    }
}

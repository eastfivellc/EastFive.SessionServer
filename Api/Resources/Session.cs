using BlackBarLabs.Extensions;
using EastFive.Security.SessionServer.Api.Resources;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace EastFive.Api.Azure.Credentials.Resources
{
    [DataContract]
    public class Session : AuthorizationRequest
    {
        [DataMember]
        [JsonProperty(PropertyName = "location_logout")]
        public Uri LocationLogout { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "location_logout_return")]
        public Uri LocationLogoutReturn { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "authorization_id")]
        public Guid? AuthorizationId { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "header_name")]
        public string HeaderName { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "token")]
        public string Token { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "refresh_token")]
        public string RefreshToken { get; set; }
        
    }
}
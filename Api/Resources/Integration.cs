using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

using Newtonsoft.Json;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class Integration:  AuthorizationRequest
    {
        [DataMember]
        [JsonProperty(PropertyName = "authorization_id")]
        public Guid AuthorizationId { get; set; }
    }
}

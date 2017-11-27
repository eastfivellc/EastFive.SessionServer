using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class AccountLink : BlackBarLabs.Api.ResourceBase
    {
        [DataMember]
        [JsonProperty(PropertyName = "login")]
        public Uri Login { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "signup")]
        public Uri Signup { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "logout")]
        public Uri Logout { get; set; }
    }
}

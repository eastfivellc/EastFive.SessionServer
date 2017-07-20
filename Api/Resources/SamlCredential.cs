using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class SamlCredential : BlackBarLabs.Api.ResourceBase
    {
        #region Properties
        
        [JsonProperty(PropertyName = "actor")]
        public WebId Actor { get; set; }
        
        [DataMember]
        [JsonProperty(PropertyName = "user_id")]
        public string UserId { get; set; }
        
        #endregion
    }
}

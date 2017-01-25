using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class PasswordCredential : BlackBarLabs.Api.ResourceBase
    {
        #region Properties
        
        [JsonProperty(PropertyName = "actor")]
        public WebId Actor { get; set; }
        
        [DataMember]
        [JsonProperty(PropertyName = "user_id")]
        public string UserId { get; set; }

        [DataMember]
        [JsonProperty("token")]
        public string Token { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "is_email")]
        public bool IsEmail { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "force_change")]
        public bool ForceChange { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "last_email_sent")]
        public DateTime? LastEmailSent { get; set; }

        #endregion
    }
}

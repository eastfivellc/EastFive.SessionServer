using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class TokenCredential : BlackBarLabs.Api.ResourceBase
    {
        #region Properties
        
        [DataMember]
        [JsonProperty("actor_id")]
        public Guid ActorId { get; set; }
        
        [DataMember]
        [JsonProperty("email")]
        public string Email { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "last_email_sent")]
        public DateTime? LastEmailSent { get; set; }

        #endregion
    }
}

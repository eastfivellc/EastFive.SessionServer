using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Security.SessionServer.Api.Resources.Queries
{
    [DataContract]
    public class PasswordCredentialQuery : BlackBarLabs.Api.ResourceQueryBase
    {
        #region Properties
        
        [DataMember]
        [JsonProperty("actor")]
        public WebIdQuery Actor { get; set; }

        [DataMember]
        [JsonProperty("token")]
        public WebIdQuery Token { get; set; }

        #endregion
    }
}

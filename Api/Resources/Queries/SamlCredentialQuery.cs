using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Security.SessionServer.Api.Resources.Queries
{
    [DataContract]
    public class SamlCredentialQuery : BlackBarLabs.Api.ResourceQueryBase
    {
        #region Properties
        
        [DataMember]
        [JsonProperty("actor")]
        public WebIdQuery Actor { get; set; }
        
        #endregion
    }
}

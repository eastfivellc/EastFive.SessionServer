using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class Monitoring : BlackBarLabs.Api.ResourceBase
    {
        #region Properties
        
        [DataMember]
        [JsonProperty("ApiKeySecurity")]
        public string ApiKeySecurity { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "month")]
        public string Month { get; set; }

        #endregion
    }
}

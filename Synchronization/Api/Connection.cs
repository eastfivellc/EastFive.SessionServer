using System.Runtime.Serialization;
using BlackBarLabs.Api;
using Newtonsoft.Json;
using BlackBarLabs.Api.Resources;

namespace EastFive.Api.Resources
{
    [DataContract]
    public class Connection : ResourceBase
    {
        [JsonProperty(PropertyName = "source")]
        public Adapter Source { get; set; }

        [JsonProperty(PropertyName = "destination")]
        public Adapter Destination { get; set; }

        [JsonProperty(PropertyName = "connector")]
        public Connector Connector { get; set; }
    }
}
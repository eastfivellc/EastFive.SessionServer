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
        public WebId Source { get; set; }

        [JsonProperty(PropertyName = "destination")]
        public WebId Destination { get; set; }

        [JsonProperty(PropertyName = "flow")]
        public string Flow { get; set; }
    }
}
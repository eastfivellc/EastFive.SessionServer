using BlackBarLabs.Api.Resources;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace EastFive.Api.Resources
{
    [DataContract]
    public class ConnectionQuery : BlackBarLabs.Api.ResourceQueryBase
    {
        [JsonProperty(PropertyName = "actor")]
        public WebIdQuery Actor { get; set; }

        [JsonProperty(PropertyName = "resource_type")]
        public string ResourceType { get; set; }
        
        [JsonProperty(PropertyName = "adapter")]
        public WebIdQuery Adapter { get; set; }

        [JsonProperty(PropertyName = "integration")]
        public WebIdQuery Integration { get; set; }
    }
}
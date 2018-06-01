using System.Runtime.Serialization;
using BlackBarLabs.Api;
using Newtonsoft.Json;
using BlackBarLabs.Api.Resources;

namespace EastFive.Api.Resources
{
    [DataContract]
    public class Adapter : ResourceBase
    {
        [JsonProperty(PropertyName = "resource_type")]
        public string ResourceType { get; set; }

        [JsonProperty(PropertyName = "resource_id")]
        public WebId ResourceId { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "keys")]
        public string[] Keys { get; set; }

        [JsonProperty(PropertyName = "values")]
        public string[] Values { get; set; }

        [JsonProperty(PropertyName = "method")]
        public string Method { get; set; }

    }
}
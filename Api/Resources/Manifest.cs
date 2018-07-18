using BlackBarLabs.Api.Resources;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace EastFive.Api.Azure.Resources
{
    [DataContract]
    public class Manifest : BlackBarLabs.Api.ResourceBase
    {
        [JsonProperty(PropertyName = "endpoints")]
        public WebId [] Endpoints { get; set; }
    }
}
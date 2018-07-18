using System;
using System.Runtime.Serialization;
using BlackBarLabs.Api;
using Newtonsoft.Json;
using BlackBarLabs.Api.Resources;

namespace EastFive.Api.Resources
{
    [DataContract]
    public class Connector : ResourceBase
    {
        public const string SourcePropertyName = "source";
        [JsonProperty(PropertyName = SourcePropertyName)]
        public WebId Source { get; set; }

        public const string DestinationPropertyName = "destination";
        [JsonProperty(PropertyName = DestinationPropertyName)]
        public WebId Destination { get; set; }
        
        public const string FlowPropertyName = "flow";
        [JsonProperty(PropertyName = FlowPropertyName)]
        public string Flow { get; set; }

        public const string CreatedByPropertyName = "created_by";
        [JsonProperty(PropertyName = CreatedByPropertyName)]
        public Guid? CreatedBy { get; set; }

        public const string DestinationIntegrationPropertyName = "destination_integration";
        [JsonProperty(PropertyName = DestinationIntegrationPropertyName)]
        public WebId DestinationIntegration { get; set; }
    }
}
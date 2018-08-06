using BlackBarLabs.Api;
using BlackBarLabs.Api.Resources;
using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace EastFive.Azure.Api.Resources
{
    [DataContract]
    public class ProcessStep : ResourceBase
    {
        public const string StagePropertyName = "stage";
        [JsonProperty(PropertyName = StagePropertyName)]
        public WebId Stage { get; set; }

        public const string ResourcePropertyName = "resource";
        [JsonProperty(PropertyName = ResourcePropertyName)]
        public WebId Resource { get; set; }

        public const string CreatedOnPropertyName = "created_on";
        [JsonProperty(PropertyName = CreatedOnPropertyName)]
        public DateTime CreatedOn { get; set; }

        public const string ResourceKeysPropertyName = "resource_keys";
        [JsonProperty(PropertyName = ResourceKeysPropertyName)]
        public string[] ResourceKeys { get; set; }

        public const string ResourcesPropertyName = "resources";
        [JsonProperty(PropertyName = ResourcesPropertyName)]
        public WebId[] Resources { get; set; }
        
        public const string ConfirmedByPropertyName = "confirmed_by";
        [JsonProperty(PropertyName = ConfirmedByPropertyName)]
        public WebId ConfirmedBy { get; set; }

        public const string ConfirmedWhenPropertyName = "confirmed_when";
        [JsonProperty(PropertyName = ConfirmedWhenPropertyName)]
        public DateTime? ConfirmedWhen { get; set; }

        public const string ConfirmedNextPropertyName = "confirmed_next";
        [JsonProperty(PropertyName = ConfirmedNextPropertyName)]
        public WebId ConfirmedNext { get; set; }
    }
}
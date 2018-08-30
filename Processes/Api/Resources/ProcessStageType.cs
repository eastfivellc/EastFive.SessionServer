using BlackBarLabs.Api;
using BlackBarLabs.Api.Resources;
using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace EastFive.Api.Azure.Resources
{
    [DataContract]
    public class ProcessStageType : ResourceBase
    {
        public const string GroupPropertyName = "group";
        [JsonProperty(PropertyName = GroupPropertyName)]
        public WebId Group { get; set; }

        public const string OwnerPropertyName = "owner";
        [JsonProperty(PropertyName = OwnerPropertyName)]
        public WebId Owner { get; set; }

        public const string TitlePropertyName = "title";
        [JsonProperty(PropertyName = TitlePropertyName)]
        public string Title { get; set; }

        public const string ResourceTypePropertyName = "resource_type";
        [JsonProperty(PropertyName = ResourceTypePropertyName)]
        public string ResourceType { get; set; }

        public const string ResourceKeysPropertyName = "resource_keys";
        [JsonProperty(PropertyName = ResourceKeysPropertyName)]
        public string [] ResourceKeys { get; set; }

        public const string ResourceTypesPropertyName = "resource_types";
        [JsonProperty(PropertyName = ResourceTypesPropertyName)]
        public string[] ResourceTypes { get; set; }
    }
}
using BlackBarLabs.Api;
using BlackBarLabs.Api.Resources;
using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace EastFive.Api.Azure.Resources
{
    [DataContract]
    public class ProcessStageGroup : ResourceBase
    {
        public const string TitlePropertyName = "title";
        [JsonProperty(PropertyName = TitlePropertyName)]
        public string Title { get; set; }

        public const string RankPropertyName = "rank";
        [JsonProperty(PropertyName = RankPropertyName)]
        public double Rank { get; set; }
    }
}
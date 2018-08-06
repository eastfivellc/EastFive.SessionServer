using BlackBarLabs.Api;
using BlackBarLabs.Api.Resources;
using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace EastFive.Azure.Api.Resources
{
    [DataContract]
    public class ProcessStage : ResourceBase
    {
        public class ConfirmableResource
        {
            public const string ProcessStageNextPropertyName = "process_stage_next";
            [JsonProperty(PropertyName = ProcessStageNextPropertyName)]
            public WebId ProcessStageNext { get; set; }

            public const string PositionsPropertyName = "positions";
            [JsonProperty(PropertyName = PositionsPropertyName)]
            public WebId [] Positions { get; set; }
        }

        public const string OwnerPropertyName = "owner";
        [JsonProperty(PropertyName = OwnerPropertyName)]
        public WebId Owner { get; set; }

        public const string TypePropertyName = "type";
        [JsonProperty(PropertyName = TypePropertyName)]
        public WebId Type { get; set; }

        public const string TitlePropertyName = "title";
        [JsonProperty(PropertyName = TitlePropertyName)]
        public string Title { get; set; }
        
        public const string ConfirmablePropertyName = "confirmable";
        [JsonProperty(PropertyName = ConfirmablePropertyName)]
        public ConfirmableResource[] Confirmable { get; set; }

        public const string EditablePropertyName = "editable";
        [JsonProperty(PropertyName = EditablePropertyName)]
        public WebId[] Editable { get; set; }

        public const string CompletablePropertyName = "completable";
        [JsonProperty(PropertyName = CompletablePropertyName)]
        public WebId[] Completable { get; set; }

        public const string ViewablePropertyName = "viewable";
        [JsonProperty(PropertyName = ViewablePropertyName)]
        public WebId[] Viewable { get; set; }
    }
}
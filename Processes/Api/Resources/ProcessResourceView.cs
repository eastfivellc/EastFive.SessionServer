using BlackBarLabs.Api;
using BlackBarLabs.Api.Resources;
using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace EastFive.Azure.Api.Resources
{
    [DataContract]
    public class ProcessResourceView : ResourceBase
    {
        #region identification of the view resources
        public const string ActorPropertyName = "actor";
        [JsonProperty(PropertyName = ActorPropertyName)]
        public WebId Actor { get; set; }

        public const string ResourcePropertyName = "resource";
        [JsonProperty(PropertyName = ResourcePropertyName)]
        public WebId Resource { get; set; }
        
        [JsonProperty(PropertyName = ProcessStageType.ResourceTypePropertyName)]
        public string ResourceType { get; set; }
        #endregion

        #region progress bar layout
        public const string CurrentProcessStepPropertyName = "current_process_step";
        [JsonProperty(PropertyName = CurrentProcessStepPropertyName)]
        public WebId CurrentProcessStep { get; set; }
        
        public const string TitlesPropertyName = "titles";
        [JsonProperty(PropertyName = TitlesPropertyName)]
        public string [] Titles { get; set; }

        public const string CompletionsPropertyName = "completions";
        [JsonProperty(PropertyName = CompletionsPropertyName)]
        public DateTime?[] Completions { get; set; }

        public const string InvalidationsPropertyName = "invalidations";
        [JsonProperty(PropertyName = InvalidationsPropertyName)]
        public DateTime?[] Invalidations { get; set; }
        #endregion

        #region resource accumulation
        public const string ResourcesDisplayedPropertyName = "resources_displayed";
        [JsonProperty(PropertyName = ResourcesDisplayedPropertyName)]
        public string[] ResourcesDisplayed { get; set; }
        
        public const string ResourcesProvidedPropertyName = "resources_provided";
        [JsonProperty(PropertyName = ResourcesProvidedPropertyName)]
        public ConfirmableResource[] ResourcesProvided { get; set; }

        public class ConfirmableResource
        {
            public const string KeyPropertyName = "key";
            [JsonProperty(PropertyName = KeyPropertyName)]
            public string Key { get; set; }
            
            [JsonProperty(PropertyName = ProcessStep.ResourcePropertyName)]
            public WebId Resource { get; set; }

            public const string TypePropertyName = "type";
            [JsonProperty(PropertyName = TypePropertyName)]
            public string Type { get; set; }
        }
        #endregion
        
        #region Available actions
        public const string NextStagesPropertyName = "next_stages";
        [JsonProperty(PropertyName = NextStagesPropertyName)]
        public WebId[] NextStages { get; set; }

        public const string EditablePropertyName = "editable";
        [JsonProperty(PropertyName = EditablePropertyName)]
        public bool Editable { get; set; }

        public const string CompletablePropertyName = "completable";
        [JsonProperty(PropertyName = CompletablePropertyName)]
        public bool Completable { get; set; }
        #endregion

    }
}
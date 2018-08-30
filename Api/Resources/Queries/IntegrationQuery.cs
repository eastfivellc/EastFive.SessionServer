using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Api.Azure.Credentials.Resources.Queries
{
    [DataContract]
    public class IntegrationQuery : BlackBarLabs.Api.ResourceQueryBase
    {
        [JsonProperty(PropertyName = "actor_id")]
        public WebIdQuery ActorId { get; set; }
    }
}

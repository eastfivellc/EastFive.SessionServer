using BlackBarLabs.Api.Resources;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace EastFive.Api.Azure.Resources.Queries
{
    [DataContract]
    public class RoleQuery : BlackBarLabs.Api.ResourceQueryBase
    {
        [JsonProperty(PropertyName = "actor")]
        public WebIdQuery Actor { get; set; }
    }
}
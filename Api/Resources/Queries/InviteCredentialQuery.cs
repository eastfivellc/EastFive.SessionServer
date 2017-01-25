using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Security.SessionServer.Api.Resources.Queries
{
    [DataContract]
    public class InviteCredentialQuery : BlackBarLabs.Api.ResourceQueryBase
    {
        [JsonProperty("actor")]
        public WebIdQuery Actor { get; set; }

        [JsonProperty("token")]
        public WebIdQuery Token { get; set; }
    }
}

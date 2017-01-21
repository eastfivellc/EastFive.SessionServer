using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Security.SessionServer.Api.Resources.Queries
{
    [DataContract]
    public class InviteQuery : BlackBarLabs.Api.ResourceQueryBase
    {
        #region Properties

        [JsonProperty("token")]
        public WebIdQuery Token { get; set; }

        #endregion
    }
}

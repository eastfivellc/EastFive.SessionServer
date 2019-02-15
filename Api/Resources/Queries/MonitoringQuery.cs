using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Api.Azure.Credentials.Resources.Queries
{
    [DataContract]
    public class MonitoringQuery : BlackBarLabs.Api.ResourceQueryBase
    {
        [JsonProperty("ApiKeySecurity")]
        public string ApiKeySecurity { get; set; }

        /// <summary>
        /// Month for which log data is requested
        /// </summary>
        [JsonProperty("month")]
        public DateTimeQuery Month { get; set; }
    }
}

using System;
using System.Linq;
using System.Runtime.Serialization;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class Authorization : BlackBarLabs.Api.ResourceBase
    {
        #region Properties

        [DataMember]
        public string DisplayName { get; set; }

        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public bool IsEmail { get; set; }

        [DataMember]
        public string Secret { get; set; }

        [DataMember]
        public bool ForceChange { get; set; }

        #endregion
    }
}

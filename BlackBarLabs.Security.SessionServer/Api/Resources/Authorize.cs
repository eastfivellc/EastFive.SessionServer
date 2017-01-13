using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Security.Session;

namespace BlackBarLabs.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class Authorize
    {
        #region Properties

        [DataMember]
        public Guid Id { get; set; }

        [DataMember]
        public Uri Scope { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Key { get; set; }

        #endregion

    }
}

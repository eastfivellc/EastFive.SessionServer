using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Security.Session;

namespace BlackBarLabs.Security.SessionServer.API.Resources
{
    [DataContract]
    public class Redirect 
    {
        #region Properties

        [DataMember]
        public Guid Id { get; set; }

        [DataMember]
        public Uri Service { get; set; }

        #endregion
        
    }
}

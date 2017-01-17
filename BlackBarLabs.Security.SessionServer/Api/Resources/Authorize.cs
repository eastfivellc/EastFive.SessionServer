using System;
using System.Linq;
using System.Runtime.Serialization;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class Authorize
    {
        #region Properties
        
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Key { get; set; }

        #endregion

    }
}

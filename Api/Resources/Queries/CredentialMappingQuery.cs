using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;

namespace EastFive.Security.SessionServer.Api.Resources.Queries
{
    [DataContract]
    public class CredentialMappingQuery : BlackBarLabs.Api.ResourceQueryBase
    {
        #region Properties
        
        [DataMember]
        public WebIdQuery Actor { get; set; }

        #endregion
    }
}

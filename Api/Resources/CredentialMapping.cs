using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class CredentialMapping : BlackBarLabs.Api.ResourceBase
    {
        #region Properties
        
        [DataMember]
        public WebId ActorId { get; set; }
        
        [DataMember]
        public WebId LoginId { get; set; }

        #endregion
    }
}

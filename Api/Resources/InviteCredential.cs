using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class InviteCredential : BlackBarLabs.Api.ResourceBase
    {
        #region Properties
        
        [DataMember]
        public Guid ActorId { get; set; }
        
        [DataMember]
        public string Email { get; set; }
        
        #endregion
    }
}

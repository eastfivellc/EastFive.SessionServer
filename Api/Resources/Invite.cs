using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class Invite : BlackBarLabs.Api.ResourceBase
    {
        #region Properties
        
        [DataMember]
        public WebId CredentialMapping { get; set; }
        
        [DataMember]
        public string Email { get; set; }
        
        #endregion
    }
}

using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class PasswordCredential : BlackBarLabs.Api.ResourceBase
    {
        #region Properties
        
        [DataMember]
        public WebId Actor { get; set; }
        
        [DataMember]
        public string UserId { get; set; }

        [DataMember]
        public string Token { get; set; }

        [DataMember]
        public bool IsEmail { get; set; }

        [DataMember]
        public bool ForceChange { get; set; }

        #endregion
    }
}

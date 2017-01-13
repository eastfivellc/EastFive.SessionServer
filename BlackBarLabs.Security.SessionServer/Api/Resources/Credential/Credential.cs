using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Security.Session;
using BlackBarLabs.Api.Resources;

namespace BlackBarLabs.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class Credential : BlackBarLabs.Api.ResourceBase
    {
        #region Properties
        
        [DataMember]
        public WebId AuthorizationId { get; set; }

        [DataMember]
        public CredentialValidationMethodTypes Method { get; set; }
        
        [DataMember]
        public string UserId { get; set; }

        [DataMember]
        public string Token { get; set; }

        [DataMember]
        public bool IsEmail { get; set; }

        #endregion
    }
}

using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Security.Session;

namespace BlackBarLabs.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class Credential : Resources.Resource, ICredential
    {
        #region Properties

        [DataMember]
        public Guid Id { get; set; }

        [DataMember]
        public Guid AuthorizationId { get; set; }

        [DataMember]
        public CredentialValidationMethodTypes Method { get; set; }

        [DataMember]
        public Uri Provider { get; set; }

        [DataMember]
        public string UserId { get; set; }

        [DataMember]
        public string Token { get; set; }
        
        #endregion

    }
}

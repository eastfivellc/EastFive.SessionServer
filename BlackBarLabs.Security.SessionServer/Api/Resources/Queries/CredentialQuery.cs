using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;

namespace EastFive.Security.SessionServer.Api.Resources.Queries
{
    [DataContract]
    public class CredentialQuery : BlackBarLabs.Api.ResourceQueryBase
    {
        #region Properties
        
        [DataMember]
        public WebIdQuery AuthorizationId { get; set; }
        
        [DataMember]
        public string UserId { get; set; }

        [DataMember]
        public string Token { get; set; }

        #endregion
    }
}

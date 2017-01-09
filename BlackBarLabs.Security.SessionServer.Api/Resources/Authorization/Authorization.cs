using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Security.Session;

namespace BlackBarLabs.Security.AuthorizationServer.API.Resources
{
    [DataContract]
    public class Authorization : Resource, IAuthorization
    {
        #region Properties

        [DataMember]
        public Guid Id { get; set; }

        [DataMember]
        public Uri[] CredentialProviders { get; set; }

        #endregion
        
        protected bool HasCredentials()
        {
            return
                this.CredentialProviders != null &&
                this.CredentialProviders.Any();
        }
    }
}

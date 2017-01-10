using System;
using System.Linq;
using System.Runtime.Serialization;

using BlackBarLabs.Security.Session;

namespace BlackBarLabs.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class Authorization : BlackBarLabs.Api.ResourceBase, IAuthorization
    {
        #region Properties
        
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

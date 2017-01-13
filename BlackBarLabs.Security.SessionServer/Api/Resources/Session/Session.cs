using BlackBarLabs.Security.Session;
using System;
using System.Runtime.Serialization;

namespace BlackBarLabs.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class Session : Resource
    {
        #region Properties

        [DataMember]
        public Guid Id { get; set; }

        [DataMember]
        public Guid AuthorizationId { get; set; }

        [DataMember]
        public AuthHeaderProps SessionHeader { get; set; }
        
        [DataMember]
        public Resources.Credential Credentials { get; set; }
        
        [DataMember]
        public string RefreshToken { get; set; }
        
        #endregion

        internal bool IsCredentialsPopulated()
        {
            if (default(Resources.Credential) == Credentials)
                return false;
            return
                (this.Credentials.Provider != default(Uri)) &&
                (!String.IsNullOrWhiteSpace(this.Credentials.UserId)) &&
                (!String.IsNullOrWhiteSpace(this.Credentials.Token));
        }
    }
}
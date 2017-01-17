using System;
using System.Runtime.Serialization;

namespace EastFive.Security.SessionServer.Api.Resources
{

    [DataContract]
    public class CredentialToken
    {
        [DataMember]
        public CredentialValidationMethodTypes Method { get; set; }

        [DataMember]
        public string Token { get; set; }
    }

    [DataContract]
    public class Session
    {
        #region Properties

        [DataMember]
        public Guid Id { get; set; }

        [DataMember]
        public Guid AuthorizationId { get; set; }

        [DataMember]
        public AuthHeaderProps SessionHeader { get; set; }
        
        [DataMember]
        public CredentialToken CredentialToken { get; set; }
        
        [DataMember]
        public string RefreshToken { get; set; }
        
        #endregion

        internal bool IsCredentialsPopulated()
        {
            if (default(CredentialToken) == this.CredentialToken)
                return false;

            return 
                !(String.IsNullOrWhiteSpace(CredentialToken.Token));
        }
    }
}
using System;

namespace BlackBarLabs.Security.SessionServer.Persistence.Azure.Documents
{
    internal class AuthorizationCheck : Microsoft.WindowsAzure.Storage.Table.TableEntity
    {
        #region Constructors
        public AuthorizationCheck() { }

        #endregion

        #region Properties
        
        public Guid AuthId { get; set; }

        public byte[] ExternalClaimsLocations { get; set; }

        #endregion
        
    }
}

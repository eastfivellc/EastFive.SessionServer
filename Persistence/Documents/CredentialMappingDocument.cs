using System;

namespace EastFive.Security.SessionServer.Persistence.Azure.Documents
{
    internal class CredentialMappingDocument : Microsoft.WindowsAzure.Storage.Table.TableEntity
    {
        #region Properties
        
        public Guid AuthId { get; set; }

        public byte[] ExternalClaimsLocations { get; set; }

        #endregion
        
    }
}

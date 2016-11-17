using System;

namespace BlackBarLabs.Security.SessionServer.Persistence.Azure.Documents
{
    internal class ClaimDocument : Microsoft.WindowsAzure.Storage.Table.TableEntity
    {
        #region Properties
        
        public Guid ClaimId { get; set; }
        public string Issuer { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }

        #endregion
    }
}

using BlackBarLabs.Persistence.Azure.Attributes;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Runtime.Serialization;

namespace EastFive.Security.SessionServer.Persistence.Documents
{
    [StorageResource(typeof(RemainderKeyGenerator), typeof(ListKeyGenerator))]
    public class CredentialMappingDocument : Microsoft.WindowsAzure.Storage.Table.TableEntity
    {
        #region Properties
        
        [IgnoreDataMember]
        [IgnoreProperty]
        public Guid Id { get { return Guid.Parse(this.RowKey); } }

        public Guid ActorId { get; set; }

        public string Method { get; set; }

        public string Subject { get; set; }

        #endregion

    }
}

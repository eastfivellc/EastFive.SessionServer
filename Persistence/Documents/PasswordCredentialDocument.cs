using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Runtime.Serialization;

namespace EastFive.Security.SessionServer.Persistence.Documents
{
    internal class PasswordCredentialDocument : Microsoft.WindowsAzure.Storage.Table.TableEntity
    {

        [IgnoreDataMember]
        [IgnoreProperty]
        public Guid Id
        {
            get { return Guid.Parse(this.RowKey); }
        }

        public Guid LoginId { get; set; }
        public DateTime? EmailLastSent { get; set; }
    }
}

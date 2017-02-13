using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using BlackBarLabs.Persistence.Azure;
using Microsoft.WindowsAzure.Storage.Table;

namespace OrderOwl.Persistence.Documents
{
    [Serializable]
    [DataContract]
    internal class RoleDocument : TableEntity
    {
        [IgnoreDataMember]
        [IgnoreProperty]
        public Guid Id { get { return Guid.Parse(this.RowKey); } }
        
        public string Name { get; set; }

        public Guid ActorId { get; set; }
    }
}
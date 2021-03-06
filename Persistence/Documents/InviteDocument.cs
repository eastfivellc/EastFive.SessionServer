﻿using System;
using System.Runtime.Serialization;

using Microsoft.WindowsAzure.Storage.Table;

namespace EastFive.Security.SessionServer.Persistence.Documents
{
    internal class InviteDocument : Microsoft.WindowsAzure.Storage.Table.TableEntity
    {
        [IgnoreDataMember]
        [IgnoreProperty]
        public Guid Id
        {
            get { return Guid.Parse(this.RowKey); }
        }

        public Guid ActorId { get; set; }
        public string Email { get; set; }
        public Guid? LoginId { get; set; }
        public bool IsToken { get; set; }
        public DateTime? LastSent { get; set; }
        public Guid Token { get; set; }
    }
}

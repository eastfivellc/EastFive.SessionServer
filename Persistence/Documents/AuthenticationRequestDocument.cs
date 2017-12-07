using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.WindowsAzure.Storage.Table;

using BlackBarLabs;
using BlackBarLabs.Collections.Async;
using BlackBarLabs.Persistence.Azure;
using BlackBarLabs.Persistence.Azure.StorageTables;
using BlackBarLabs.Linq;

namespace EastFive.Security.SessionServer.Persistence.Documents
{
    internal class AuthenticationRequestDocument : Microsoft.WindowsAzure.Storage.Table.TableEntity
    {
        [IgnoreDataMember]
        [IgnoreProperty]
        public Guid Id
        {
            get { return Guid.Parse(this.RowKey); }
        }

        #region Properties

        public string Method { get; set; }
        public string Token { get; set; }

        public byte[] ExtraParams { get; set; }
        public string Action { get; set; }
        public Guid? LinkedAuthenticationId { get; set; }
        public string RedirectUrl { get; set; }

        internal IDictionary<string, string> GetExtraParams()
        {
            return ExtraParams.FromByteArray(
                (keyBytes) => System.Text.Encoding.UTF8.GetString(keyBytes),
                (valueBytes) => System.Text.Encoding.UTF8.GetString(valueBytes));
        }

        internal void SetExtraParams(IDictionary<string, string> extraParams)
        {
            ExtraParams = extraParams.ToByteArray(
                (key) => System.Text.Encoding.UTF8.GetBytes(key),
                (value) => System.Text.Encoding.UTF8.GetBytes(value));
        }

        #endregion
    }
}

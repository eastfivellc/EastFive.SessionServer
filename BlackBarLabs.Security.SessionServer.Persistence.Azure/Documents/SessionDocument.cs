using System;

namespace BlackBarLabs.Security.SessionServer.Persistence.Azure.Documents
{
    [Serializable]
    internal class SessionDocument : Microsoft.WindowsAzure.Storage.Table.TableEntity
    {
        public Guid AuthorizationId { get; set; }
        public string RefreshToken { get; set; }
        public Guid SessionId { get; set; }
    }
}

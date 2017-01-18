using System;

namespace EastFive.Security.SessionServer.Persistence.Azure.Documents
{
    internal class CredentialRedirectDocument : Microsoft.WindowsAzure.Storage.Table.TableEntity
    {
        #region Properties

        #endregion
        public Guid ActorId { get; set; }
        public string Email { get; set; }
        public bool Redeemed { get; set; }
    }
}

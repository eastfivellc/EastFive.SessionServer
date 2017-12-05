using System;

namespace EastFive.Security.SessionServer.Persistence
{
    public class DataContext : BlackBarLabs.Persistence.Azure.DataStores
    {
        public DataContext(string appAzureTableStorageSettingsKey) : base(appAzureTableStorageSettingsKey)
        {
        }
        
        private Sessions sessions = null;
        public Sessions Sessions
        {
            get
            {
                if (default(Sessions) == sessions)
                    sessions = new Sessions(this.AzureStorageRepository);
                return sessions;
            }
        }

        private CredentialMappings credentialMappings = null;
        public CredentialMappings CredentialMappings
        {
            get
            {
                if (default(CredentialMappings) == credentialMappings)
                    credentialMappings = new CredentialMappings(this.AzureStorageRepository);
                return credentialMappings;
            }
        }

        private PasswordCredentials passwordCredentials = null;
        public PasswordCredentials PasswordCredentials
        {
            get
            {
                if (default(PasswordCredentials) == passwordCredentials)
                    passwordCredentials = new PasswordCredentials(this, this.AzureStorageRepository);
                return passwordCredentials;
            }
        }

        private Claims claims = null;
        public Claims Claims
        {
            get
            {
                if (default(Claims) == claims)
                    claims = new Claims(this.AzureStorageRepository);
                return claims;
            }
        }

        private Accesses accesses = null;
        public Accesses Accesses
        {
            get
            {
                if (default(Accesses) == accesses)
                    accesses = new Accesses(this.AzureStorageRepository);
                return accesses;
            }
        }

        private Roles roles = default(Roles);
        public Roles Roles
        {
            get
            {
                if (default(Roles) == roles)
                    roles = new Roles(this);
                return roles;
            }
        }

        private AuthenticationRequests authenticationRequests = default(AuthenticationRequests);
        public AuthenticationRequests AuthenticationRequests
        {
            get
            {
                if (default(AuthenticationRequests) == authenticationRequests)
                    authenticationRequests = new AuthenticationRequests(this, this.AzureStorageRepository);
                return authenticationRequests;
            }
        }
    }
}

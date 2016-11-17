

namespace BlackBarLabs.Security.SessionServer.Persistence.Azure
{
    public class DataContext : BlackBarLabs.Persistence.Azure.DataStores, IDataContext
    {
        public DataContext(string appAzureTableStorageSettingsKey) : base(appAzureTableStorageSettingsKey)
        {
        }

        private Authorizations authorizations = null;
        public IAuthorizations Authorizations
        {
            get
            {
                if (default(Authorizations) == authorizations)
                    authorizations = new Authorizations(this.AzureStorageRepository);
                return authorizations;
            }
        }

        private Sessions sessions = null;
        public ISessions Sessions
        {
            get
            {
                if (default(Sessions) == sessions)
                    sessions = new Sessions(this.AzureStorageRepository);
                return sessions;
            }
        }
    }
}

using EastFive.Web;

namespace EastFive.Azure.Persistence
{
    [ConfigAttribute]
    public static class AppSettings
    {
        [ConfigKey("Default azure storage tables connection string",
            DeploymentOverrides.Suggested,
            DeploymentSecurityConcern = false,
            PrivateRepositoryOnly = true)]
        public const string Storage = "EastFive.Azure.StorageTables.ConnectionString";

        [ConfigKey("Default azure spa connection string",
            DeploymentOverrides.Suggested,
            DeploymentSecurityConcern = false,
            PrivateRepositoryOnly = true)]
        public const string SpaStorage = "EastFive.Azure.Spa.ConnectionString";

        [ConfigKey("Default azure spa container name",
            DeploymentOverrides.Suggested,
            DeploymentSecurityConcern = false,
            PrivateRepositoryOnly = true)]
        public const string SpaContainer = "EastFive.Azure.Spa.ContainerName";
    }
}

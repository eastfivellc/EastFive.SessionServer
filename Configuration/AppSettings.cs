namespace EastFive.Security.SessionServer.Configuration
{
    public static class AppSettings
    {
        public const string Storage = "Azure.Authorization.Storage";
        //public const string Storage = "EastFive.Security.SessionServer.Storage";
        public const string TokenExpirationInMinutes = "EastFive.Security.SessionServer.tokenExpirationInMinutes";
        public const string LoginIdClaimType = "EastFive.Security.SessionServer.LoginProvider.LoginIdClaimType";

        public const string AADB2CAudience = "EastFive.Security.LoginProvider.AzureADB2C.Audience";
        public const string AADB2CSigninConfiguration = "EastFive.Security.LoginProvider.AzureADB2C.SigninEndpoint";
        public const string AADB2CSignupConfiguration = "EastFive.Security.LoginProvider.AzureADB2C.SignupEndpoint";

        public const string PingIdentityAthenaRestApiKey = "EastFive.Security.LoginProvider.PingIdentity.Athena.RestApiKey";
        public const string PingIdentityAthenaRestAuthUsername = "EastFive.Security.LoginProvider.PingIdentity.Athena.RestAuthUsername";
        

        public const string LandingPage = "EastFive.Security.SessionServer.RouteDefinitions.LandingPage";
        public const string AppleAppSiteAssociationId = "EastFive.Security.SessionServer.AppleAppSiteAssociation.AppId";
        
        public static class TokenCredential
        {
            /// <summary>
            /// The email address and name from which a token credential is sent.
            /// </summary>
            public const string FromEmail = "EastFive.Security.SessionServer.TokenCredential.FromEmail";
            public const string FromName = "EastFive.Security.SessionServer.TokenCredential.FromName";
            /// <summary>
            /// Subject for token credntial email.
            /// </summary>
            public const string Subject = "EastFive.Security.SessionServer.TokenCredential.Subject";
        }
    }
}

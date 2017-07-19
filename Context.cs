using EastFive.Api.Services;
using EastFive.Security.LoginProvider;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using BlackBarLabs.Persistence.Azure;
using EastFive.Security.CredentialProvider;

namespace EastFive.Security.SessionServer
{
    public class Context
    {
        private SessionServer.Persistence.DataContext dataContext;
        private readonly Func<SessionServer.Persistence.DataContext> dataContextCreateFunc;

        private ConcurrentDictionary<CredentialValidationMethodTypes, IProvideCredentials> credentialProviders = 
            new ConcurrentDictionary<CredentialValidationMethodTypes, IProvideCredentials>();

        public Context(Func<SessionServer.Persistence.DataContext> dataContextCreateFunc,
            Func<Task<IIdentityService>> getLoginProvider,
            Func<ISendMessageService> getMailService)
        {
            dataContextCreateFunc.ValidateArgumentIsNotNull("dataContextCreateFunc");
            this.dataContextCreateFunc = dataContextCreateFunc;
            
            //getLoginProvider.ValidateArgumentIsNotNull("getLoginProvider");
            this.loginProviderFunc = getLoginProvider;

            this.mailServiceFunc = getMailService;
        }

        internal SessionServer.Persistence.DataContext DataContext
        {
            get { return dataContext ?? (dataContext = dataContextCreateFunc.Invoke()); }
        }

        #region Services

        private Func<Task<IIdentityService>> loginProviderFunc;
        private Task<IIdentityService> loginProvider;
        internal Task<IIdentityService> LoginProvider
        {
            get { return loginProvider ?? (loginProvider = loginProviderFunc.Invoke()); }
        }

        public async Task<TResult> CreateOrUpdateClaim<TResult>(Guid accountId, string claimType, string claimValue,
            Func<TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            var claimId = (accountId + claimType).MD5HashGuid();
            return await this.Claims.CreateOrUpdateAsync(accountId, claimId, claimType, claimValue,
                onSuccess,
                () => onFailure("Account was not found"),
                () => onFailure("Claim is already in use"));
        }

        internal async Task<IProvideCredentials> GetCredentialProvider(CredentialValidationMethodTypes method)
        {
            if (!this.credentialProviders.ContainsKey(method))
            {
                var newProvider = default(IProvideCredentials);
                switch (method)
                {
                    case CredentialValidationMethodTypes.SAML:
                        newProvider = new EastFive.Security.SessionServer.CredentialProvider.SAML.SAMLProvider();
                        break;
                    case CredentialValidationMethodTypes.Password:
                        newProvider = new Security.CredentialProvider.AzureADB2C.AzureADB2CProvider(await this.LoginProvider, this);
                        break;
                    case CredentialValidationMethodTypes.Voucher:
                        newProvider = new Security.CredentialProvider.Voucher.VoucherCredentialProvider();
                        break;
                    case CredentialValidationMethodTypes.Token:
                        newProvider = new Security.CredentialProvider.Token.TokenCredentialProvider(this.dataContext);
                        break;
                }
                this.credentialProviders.AddOrUpdate(method, newProvider, (m, p) => newProvider);
            }
            var provider = this.credentialProviders[method];
            return provider;
        }

        private Func<ISendMessageService> mailServiceFunc;
        private ISendMessageService mailService;
        internal ISendMessageService MailService
        {
            get { return mailService ?? (mailService = mailServiceFunc.Invoke()); }
        }

        #endregion
        
        private Credentials credentialMappings;
        public Credentials CredentialMappings
        {
            get
            {
                if (default(Credentials) == credentialMappings)
                    credentialMappings = new Credentials(this, this.DataContext);
                return credentialMappings;
            }
        }

        private PasswordCredentials passwordCredentials;
        public PasswordCredentials PasswordCredentials
        {
            get
            {
                if (default(PasswordCredentials) == passwordCredentials)
                    passwordCredentials = new PasswordCredentials(this, this.DataContext);
                return passwordCredentials;
            }
        }

        private Sessions sessions;
        public Sessions Sessions
        {
            get
            {
                if (default(Sessions) == sessions)
                    sessions = new Sessions(this, this.DataContext);
                return sessions;
            }
        }

        private Authorizations authorizations;
        public Authorizations Authorizations
        {
            get
            {
                if (default(Authorizations) == authorizations)
                    authorizations = new Authorizations(this, this.DataContext);
                return authorizations;
            }
        }

        private Claims claims;
        public Claims Claims
        {
            get
            {
                if (default(Claims) == claims)
                    claims = new Claims(this, this.DataContext);
                return claims;
            }
        }

        private Roles roles;
        public Roles Roles
        {

            get
            {
                if (default(Roles) == roles)
                    roles = new Roles(this, DataContext);
                return roles;
            }
        }


        #region Authorizations

        public delegate bool GetCredentialDelegate(CredentialValidationMethodTypes validationMethod, Uri provider, string userId, string userToken);
        
        #endregion



    }
}

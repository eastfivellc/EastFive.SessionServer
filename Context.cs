﻿using EastFive.Api.Services;
using EastFive.Security.LoginProvider;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace EastFive.Security.SessionServer
{
    public class Context
    {
        private SessionServer.Persistence.DataContext dataContext;
        private readonly Func<SessionServer.Persistence.DataContext> dataContextCreateFunc;

        private ConcurrentDictionary<CredentialValidationMethodTypes, CredentialProvider.IProvideCredentials> credentialProviders = 
            new ConcurrentDictionary<CredentialValidationMethodTypes, CredentialProvider.IProvideCredentials>();

        public Context(Func<SessionServer.Persistence.DataContext> dataContextCreateFunc,
            Func<Task<IIdentityService>> getLoginProvider,
            Func<ISendMessageService> getMailService)
        {
            dataContextCreateFunc.ValidateArgumentIsNotNull("dataContextCreateFunc");
            this.dataContextCreateFunc = dataContextCreateFunc;
            
            getLoginProvider.ValidateArgumentIsNotNull("getLoginProvider");
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

        internal async Task<CredentialProvider.IProvideCredentials> GetCredentialProvider(CredentialValidationMethodTypes method)
        {
            if (!this.credentialProviders.ContainsKey(method))
            {
                var newProvider = new CredentialProvider.AzureADB2C.AzureADB2CProvider(await this.LoginProvider, this);
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
                //if (default(Claims) == claims)
                //    claims = new Claims(this, this.DataContext);
                return claims;
            }
        }


        #region Authorizations

        public delegate bool GetCredentialDelegate(CredentialValidationMethodTypes validationMethod, Uri provider, string userId, string userToken);
        
        #endregion



    }
}

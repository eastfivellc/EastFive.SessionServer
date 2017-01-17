using EastFive.Security.LoginProvider;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace EastFive.Security.SessionServer
{
    public class Context
    {
        private SessionServer.Persistence.Azure.DataContext dataContext;
        private readonly Func<SessionServer.Persistence.Azure.DataContext> dataContextCreateFunc;

        private ConcurrentDictionary<CredentialValidationMethodTypes, CredentialProvider.IProvideCredentials> credentialProviders = 
            new ConcurrentDictionary<CredentialValidationMethodTypes, CredentialProvider.IProvideCredentials>();
        private readonly Func<CredentialValidationMethodTypes, CredentialProvider.IProvideCredentials> credentialProvidersFunc;

        public Context(Func<SessionServer.Persistence.Azure.DataContext> dataContextCreateFunc,
            Func<CredentialValidationMethodTypes, CredentialProvider.IProvideCredentials> credentialProvidersFunc,
            Func<IProvideLogin> getLoginProvider)
        {
            dataContextCreateFunc.ValidateArgumentIsNotNull("dataContextCreateFunc");
            this.dataContextCreateFunc = dataContextCreateFunc;

            credentialProvidersFunc.ValidateArgumentIsNotNull("credentialProvidersFunc");
            this.credentialProvidersFunc = credentialProvidersFunc;

            getLoginProvider.ValidateArgumentIsNotNull("getLoginProvider");
            this.loginProviderFunc = getLoginProvider;
        }

        internal SessionServer.Persistence.Azure.DataContext DataContext
        {
            get { return dataContext ?? (dataContext = dataContextCreateFunc.Invoke()); }
        }

        private Func<IProvideLogin> loginProviderFunc;
        private IProvideLogin loginProvider;
        internal IProvideLogin LoginProvider
        {
            get { return loginProvider ?? (loginProvider = loginProviderFunc.Invoke()); }
        }

        internal CredentialProvider.IProvideCredentials GetCredentialProvider(CredentialValidationMethodTypes method)
        {
            if (!this.credentialProviders.ContainsKey(method))
            {
                var newProvider = this.credentialProvidersFunc.Invoke(method);
                this.credentialProviders.AddOrUpdate(method, newProvider, (m, p) => newProvider);
            }
            var provider = this.credentialProviders[method];
            return provider;
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


        #region Authorizations

        public delegate bool GetCredentialDelegate(CredentialValidationMethodTypes validationMethod, Uri provider, string userId, string userToken);
        
        #endregion



    }
}

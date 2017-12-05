using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using BlackBarLabs.Persistence.Azure;
using EastFive.Security.CredentialProvider;
using EastFive.Api.Services;
using BlackBarLabs;
using BlackBarLabs.Extensions;
using BlackBarLabs.Linq;
using BlackBarLabs.Collections.Generic;
using BlackBarLabs.Api;
using EastFive.Collections.Generic;

namespace EastFive.Security.SessionServer
{
    public class Context
    {
        private Security.SessionServer.Persistence.DataContext dataContext;
        private readonly Func<Security.SessionServer.Persistence.DataContext> dataContextCreateFunc;
        
        public Context(Func<Security.SessionServer.Persistence.DataContext> dataContextCreateFunc)
        {
            dataContextCreateFunc.ValidateArgumentIsNotNull("dataContextCreateFunc");
            this.dataContextCreateFunc = dataContextCreateFunc;
        }

        internal Security.SessionServer.Persistence.DataContext DataContext
        {
            get { return dataContext ?? (dataContext = dataContextCreateFunc.Invoke()); }
        }

        #region Services
        
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

        internal TResult GetCredentialProvider<TResult>(CredentialValidationMethodTypes method,
            Func<IProvideAuthorization, TResult> onSuccess,
            Func<TResult> onCredintialSystemNotAvailable,
            Func<string, TResult> onFailure)
        {
            if (ServiceConfiguration.credentialProviders.IsDefault())
                return onFailure("Authentication system not initialized.");

            if (!ServiceConfiguration.credentialProviders.ContainsKey(method))
                return GetLoginProvider(method, onSuccess, onCredintialSystemNotAvailable, onFailure);

            var provider = ServiceConfiguration.credentialProviders[method];
            return onSuccess(provider);
        }

        internal TResult GetLoginProvider<TResult>(CredentialValidationMethodTypes method,
            Func<IProvideLogin, TResult> onSuccess,
            Func<TResult> onCredintialSystemNotAvailable,
            Func<string, TResult> onFailure)
        {
            if (ServiceConfiguration.loginProviders.IsDefault())
                return onFailure("Authentication system not initialized.");

            if (!ServiceConfiguration.loginProviders.ContainsKey(method))
                return onCredintialSystemNotAvailable();

            var provider = ServiceConfiguration.loginProviders[method];
            return onSuccess(provider);
        }

        internal TResult GetAccessProvider<TResult>(CredentialValidationMethodTypes method,
            Func<IProvideAccess, TResult> onSuccess,
            Func<TResult> onCredintialSystemNotAvailable,
            Func<string, TResult> onFailure)
        {
            if (ServiceConfiguration.accessProviders.IsDefault())
                return onFailure("Authentication system not initialized.");

            if (!ServiceConfiguration.accessProviders.ContainsKey(method))
                return onCredintialSystemNotAvailable();

            var provider = ServiceConfiguration.accessProviders[method];
            return onSuccess(provider);
        }

        internal TResult GetLoginProviders<TResult>(
            Func<IProvideLogin[], TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            if (ServiceConfiguration.loginProviders.IsDefault())
                return onFailure("Authentication system not initialized.");
            
            return onSuccess(ServiceConfiguration.loginProviders.SelectValues().ToArray());
        }

        internal TResult GetManagementProvider<TResult>(CredentialValidationMethodTypes method,
            Func<IProvideLoginManagement, TResult> onSuccess,
            Func<TResult> onCredintialSystemNotAvailable,
            Func<string, TResult> onFailure)
        {
            if (ServiceConfiguration.managementProviders.IsDefault())
                return onFailure("Authentication system not initialized.");

            if (!ServiceConfiguration.managementProviders.ContainsKey(method))
                return onCredintialSystemNotAvailable();

            var provider = ServiceConfiguration.managementProviders[method];
            return onSuccess(provider);
        }
        
        #endregion

        private Credentials credentialMappings;
        public Credentials Credentials
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

        private Accesses accesses;
        public Accesses Accesses
        {
            get
            {
                if (default(Accesses) == accesses)
                    accesses = new Accesses(this, this.DataContext);
                return accesses;
            }
        }
        
        private AuthenticationRequests authenticationRequests;
        public AuthenticationRequests AuthenticationRequests
        {
            get
            {
                if (default(AuthenticationRequests) == authenticationRequests)
                    authenticationRequests = new AuthenticationRequests(this, this.DataContext);
                return authenticationRequests;
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
        
        private LoginProviders loginProviders;
        public LoginProviders LoginProviders
        {

            get
            {
                if (loginProviders.IsDefault())
                    loginProviders = new LoginProviders(this, DataContext);
                return loginProviders;
            }
        }

        private Health health;
        public Health Health
        {

            get
            {
                if (default(Health) == health)
                    health = new Health(this, DataContext);
                return health;
            }
        }

        #region Authorizations

        public delegate bool GetCredentialDelegate(CredentialValidationMethodTypes validationMethod, Uri provider, string userId, string userToken);
        
        #endregion
    }
}

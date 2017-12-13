using System;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;

using BlackBarLabs.Extensions;
using BlackBarLabs.Api;
using BlackBarLabs.Linq.Async;
using BlackBarLabs.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using EastFive.Collections.Generic;

namespace EastFive.Security.SessionServer
{
    public struct LoginProvider
    {
        public Guid id;
        internal Uri loginUrl;
        internal Uri signupUrl;
        internal Uri logoutUrl;
    }

    public class LoginProviders
    {
        private Context context;
        private Persistence.DataContext dataContext;

        internal LoginProviders(Context context, Persistence.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }

        public Task<TResult> GetAllAsync<TResult>(
            Func<CredentialValidationMethodTypes[], TResult> onSuccess)
        {
            return onSuccess(ServiceConfiguration.loginProviders.SelectKeys().ToArray()).ToTask();
        }

        public Task<TResult> GetAllAsync<TResult>(bool integrationOnly,
            Func<CredentialValidationMethodTypes[], TResult> onSuccess)
        {
            if (!integrationOnly)
                return GetAllAsync(onSuccess);
            return onSuccess(ServiceConfiguration.accessProviders.SelectKeys().ToArray()).ToTask();
        }


    }
}

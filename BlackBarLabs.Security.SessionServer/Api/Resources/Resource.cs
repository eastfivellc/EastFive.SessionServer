using BlackBarLabs.Security.Session;
using BlackBarLabs.Security.SessionServer.Persistence.Azure;
using System;
using BlackBarLabs.Security.SessionServer;
using BlackBarLabs.Security.CredentialProvider.ImplicitCreation;

namespace BlackBarLabs.Security.SessionServer.Api.Resources
{
    public class Resource : BlackBarLabs.Api.Resource
    {
        private static readonly object DataContextLock = new object();

        private Context context;

        protected Context Context
        {
            get
            {
                if (default(Context) == context)
                {
                    lock (DataContextLock)
                    {
                        context = GetContext();
                    }
                }

                return context;
            }
        }

        private static Context GetContext()
        {
            var context = new Context(() => new DataContext("Azure.Authorization.Storage"),
                (credentialValidationMethodType) =>
                {
                    switch (credentialValidationMethodType)
                    {
                        case CredentialValidationMethodTypes.Facebook:
                            return new CredentialProvider.Facebook.FacebookCredentialProvider();
                        case CredentialValidationMethodTypes.Implicit:
                            return new ImplicitlyCreatedCredentialProvider();
                        case CredentialValidationMethodTypes.Voucher:
                            return new CredentialProvider.Voucher.VoucherCredentialProvider();
                        default:
                            break;
                    }
                    return new CredentialProvider.OpenIdConnect.OpenIdConnectCredentialProvider();
                });
            return context;
        }
    }
}
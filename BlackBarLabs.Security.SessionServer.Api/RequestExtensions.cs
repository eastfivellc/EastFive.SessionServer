using System;
using System.Net.Http;

using BlackBarLabs.Security.Session;
using BlackBarLabs.Security.CredentialProvider.ImplicitCreation;
using BlackBarLabs.Security.SessionServer.Persistence.Azure;

namespace BlackBarLabs.Security.SessionServer
{
    internal static class RequestExtensions
    {
        internal static AuthorizationServer.Context GetSessionServerContext(this HttpRequestMessage request)
        {
            var context = new AuthorizationServer.Context(() => new DataContext("Azure.Authorization.Storage"),
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
using System;
using System.Net.Http;

using EastFive.Security.CredentialProvider.ImplicitCreation;
using EastFive.Security.SessionServer.Persistence.Azure;

using BlackBarLabs.Api.Extensions;

namespace EastFive.Security.SessionServer
{
    internal static class RequestExtensions
    {
        internal static SessionServer.Context GetSessionServerContext(this HttpRequestMessage request)
        {
            var loginProvider = (Func<LoginProvider.IProvideLogin>)
                request.Properties[BlackBarLabs.Api.ServicePropertyDefinitions.IdentityService];
            var context = new SessionServer.Context(() => new DataContext("Azure.Authorization.Storage"),
                (credentialValidationMethodType) =>
                {
                    switch (credentialValidationMethodType)
                    {
                        case CredentialValidationMethodTypes.AzureADB2C:
                            {
                                // Catch this in the default
                                break;
                            }
                        case CredentialValidationMethodTypes.Voucher:
                            return new CredentialProvider.Voucher.VoucherCredentialProvider();
                        default:
                            break;
                    }

                    object identityServiceCreateObject;
                    request.Properties.TryGetValue(
                        BlackBarLabs.Api.ServicePropertyDefinitions.IdentityService, out identityServiceCreateObject);
                    var identityServiceCreate = (Func<EastFive.Security.LoginProvider.IProvideLogin>)identityServiceCreateObject;
                    var service = identityServiceCreate();
                    return new CredentialProvider.AzureADB2C.AzureADB2CProvider(service);
                }, loginProvider);
            return context;
        }
    }
}
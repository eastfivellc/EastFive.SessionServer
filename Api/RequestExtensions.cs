using System;
using System.Net.Http;
using System.Threading.Tasks;

using EastFive.Security.SessionServer.Persistence;

using EastFive.Api.Services;

namespace EastFive.Security.SessionServer
{
    internal static class RequestExtensions
    {
        internal static SessionServer.Context GetSessionServerContext(this HttpRequestMessage request)
        {
            object identityServiceCreateObject;
            request.Properties.TryGetValue(
                BlackBarLabs.Api.ServicePropertyDefinitions.IdentityService, out identityServiceCreateObject);
            var identityServiceCreate = (Func<Task<IIdentityService>>)identityServiceCreateObject;

            object mailServiceObject;
            request.Properties.TryGetValue(
                BlackBarLabs.Api.ServicePropertyDefinitions.MailService, out mailServiceObject);
            var mailService = (Func<ISendMessageService>)mailServiceObject;

            var context = new SessionServer.Context(() => new DataContext("Azure.Authorization.Storage"),
                // TODO: Remove this injection
                async (credentialValidationMethodType) =>
                {
                    switch (credentialValidationMethodType)
                    {
                        case CredentialValidationMethodTypes.Password:
                            {
                                // Catch this in the default
                                break;
                            }
                        case CredentialValidationMethodTypes.Voucher:
                            return new CredentialProvider.Voucher.VoucherCredentialProvider();
                        default:
                            break;
                    }

                    var identityServiceTask = identityServiceCreate();
                    var identityService = await identityServiceTask;
                    return new CredentialProvider.AzureADB2C.AzureADB2CProvider(identityService);
                }, identityServiceCreate, mailService);
            return context;
        }
    }
}
using EastFive.Api.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Controllers;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class BaseController : BlackBarLabs.Api.Controllers.BaseController
    {
        private static LoginProvider.AzureADB2C.LoginProvider loginProvider =
            default(LoginProvider.AzureADB2C.LoginProvider); 
        protected static async Task<LoginProvider.AzureADB2C.LoginProvider> GetLoginProviderAsync()
        {
            if (default(LoginProvider.AzureADB2C.LoginProvider) == loginProvider)
            {
                loginProvider = new LoginProvider.AzureADB2C.LoginProvider(null);
                await loginProvider.InitializeAsync();
            }
            return loginProvider;
        }

        private static Func<ISendMessageService> messageService =
            default(Func<ISendMessageService>);

        internal static void SetMessageService(Func<ISendMessageService> messageService)
        {
            BaseController.messageService = messageService;
        }

        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);

            Func<Task<IIdentityService>> callback = 
                async () =>
                {
                    return await GetLoginProviderAsync();
                };
            if (controllerContext.Request.Properties.ContainsKey(
                BlackBarLabs.Api.ServicePropertyDefinitions.IdentityService))
            {
                controllerContext.Request.Properties[
                    BlackBarLabs.Api.ServicePropertyDefinitions.IdentityService] = callback;
            } else
                controllerContext.Request.Properties.Add(
                    BlackBarLabs.Api.ServicePropertyDefinitions.IdentityService, callback);
            
            if (controllerContext.Request.Properties.ContainsKey(
                BlackBarLabs.Api.ServicePropertyDefinitions.MailService))
            {
                controllerContext.Request.Properties[
                    BlackBarLabs.Api.ServicePropertyDefinitions.MailService] = messageService;
            }
            else
                controllerContext.Request.Properties.Add(
                    BlackBarLabs.Api.ServicePropertyDefinitions.MailService, messageService);
        }
    }
}

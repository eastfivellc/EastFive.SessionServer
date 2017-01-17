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
        protected static LoginProvider.AzureADB2C.LoginProvider GetLoginProvider()
        {
            if (default(LoginProvider.AzureADB2C.LoginProvider) == loginProvider)
                loginProvider = new LoginProvider.AzureADB2C.LoginProvider();
            return loginProvider;
        }

        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);
            Func<LoginProvider.IProvideLogin> callback = () =>
                {
                    return GetLoginProvider();
                };
            if (controllerContext.Request.Properties.ContainsKey(
                BlackBarLabs.Api.ServicePropertyDefinitions.IdentityService))
            {
                controllerContext.Request.Properties[
                    BlackBarLabs.Api.ServicePropertyDefinitions.IdentityService] = callback;
                return;
            }
            controllerContext.Request.Properties.Add(
                BlackBarLabs.Api.ServicePropertyDefinitions.IdentityService, callback);
        }
    }
}

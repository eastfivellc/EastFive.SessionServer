using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using EastFive.Api;
using EastFive.Security.SessionServer;
using EastFive.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Controllers
{
    [FunctionViewController(Route = "SheetIntegration")]
    public static class SheetIntegrationController
    {
        [HttpGet]
        public async static Task<HttpResponseMessage> IntegrationUploadAsync(EastFive.Security.SessionServer.Context context,
                [RequiredAndAvailableInPath]Guid integration, HttpRequestMessage request, System.Web.Http.Routing.UrlHelper url,
            ViewFileResponse onLoadUploadPage)
        {
            return await onLoadUploadPage("SheetIntegration/UploadSheet.cshtml", null).ToTask();
        }

        [HttpPost]
        public async static Task<HttpResponseMessage> XlsPostAsync(EastFive.Security.SessionServer.Context context,
                System.IO.Stream sheet, [Required]Guid integration, IDictionary<string, bool> resourceTypes,
                HttpRequestMessage request, System.Web.Http.Routing.UrlHelper url,
            UnauthorizedResponse onUnauthorized,
            RedirectResponse onSuccess,
            ViewFileResponse onError)
        {
            return onUnauthorized();
            // return onSuccess(new Uri("http://example.com"));
        }
    }
}

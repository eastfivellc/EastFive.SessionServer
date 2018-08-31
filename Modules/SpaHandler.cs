using BlackBarLabs.Extensions;
using EastFive.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using EastFive.Serialization;
using System.Net.NetworkInformation;
using EastFive.Extensions;
using BlackBarLabs.Web;
using Microsoft.ApplicationInsights;
using EastFive.Api;
using System.Net.Http;
using System.Threading;
using BlackBarLabs.Api;

namespace EastFive.Api.Azure.Modules
{
    public class SpaHandler : EastFive.Api.Modules.ApplicationHandler
    {
        internal const string IndexHTMLFileName = "index.html";

        private Dictionary<string, byte[]> lookupSpaFile;
        static internal byte[] indexHTML;
        private string[] firstSegments;

        public SpaHandler(System.Web.Http.HttpConfiguration config)
            : base(config)
        {
            // TODO: A better job of matching that just grabbing the first segment
            firstSegments = System.Web.Routing.RouteTable.Routes
                .Where(route => route is System.Web.Routing.Route)
                .Select(route => route as System.Web.Routing.Route)
                .Where(route => !route.Url.IsNullOrWhiteSpace())
                .Select(
                    route => route.Url.Split(new char[] { '/' }).First())
                .ToArray();
        }
        
        private void ExtractSpaFiles(AzureApplication application, HttpRequest request)
        {
            var spaZipPath = System.Web.Hosting.HostingEnvironment.MapPath("~/Content/Spa.zip");
            if (!System.IO.File.Exists(spaZipPath))
            {
                lookupSpaFile = new Dictionary<string, byte[]>();
                indexHTML = System.Text.Encoding.UTF8.GetBytes("SPA Not Installed");
                return;
            };

            using (var zipArchive = ZipFile.OpenRead(spaZipPath))
            {

                indexHTML = zipArchive.Entries
                    .First(item => string.Compare(item.FullName, IndexHTMLFileName, true) == 0)
                    .Open()
                    .ToBytes();

                lookupSpaFile = ConfigurationContext.Instance.GetSettingValue(Security.SessionServer.Constants.AppSettingKeys.SpaSiteLocation,
                    (siteLocation) =>
                    {
                        application.Telemetry.TrackEvent($"SpaHandlerModule - ExtractSpaFiles   siteLocation: {siteLocation}");
                        return zipArchive.Entries
                            .Where(item => string.Compare(item.FullName, IndexHTMLFileName, true) != 0)
                            .Select(
                                entity =>
                                {
                                    if (!entity.FullName.EndsWith(".js"))
                                        return entity.FullName.PairWithValue(entity.Open().ToBytes());

                                    var fileBytes = entity.Open()
                                        .ToBytes()
                                        .GetString()
                                        .Replace("8FCC3D6A-9C25-4802-8837-16C51BE9FDBE.example.com", siteLocation)
                                        .GetBytes();

                                    return entity.FullName.PairWithValue(fileBytes);

                                })
                            .ToDictionary();
                    },
                    () =>
                    {
                        application.Telemetry.TrackException(new ArgumentNullException("Could not find SpaSiteLocation - is this key set in app settings?"));
                        return new Dictionary<string, byte[]>();
                    });
            }
        }

        
        protected override async Task<HttpResponseMessage> SendAsync(EastFive.Api.HttpApplication httpApp, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var context = httpApp.Context;
            string filePath = context.Request.FilePath;
            string fileName = VirtualPathUtility.GetFileName(filePath);

            if (!(httpApp is AzureApplication))
                return await base.SendAsync(request, cancellationToken);

            if (lookupSpaFile.IsDefault())
                ExtractSpaFiles(httpApp as AzureApplication, context.Request);

            if (lookupSpaFile.ContainsKey(fileName))
                return request.CreateContentResponse(lookupSpaFile[fileName],
                    fileName.EndsWith(".js")?
                        "text/javascript"
                        :
                        fileName.EndsWith(".css")?
                            "text/css"
                            :
                            request.Headers.Accept.Any()?
                                request.Headers.Accept.First().MediaType
                                :
                                string.Empty);

            var requestStart = request.RequestUri.AbsolutePath.ToLower();
            if (!firstSegments
                    .Where(firstSegment => requestStart.StartsWith($"/{firstSegment}"))
                    .Any())
                return request.CreateHtmlResponse(Security.SessionServer.Properties.Resources.indexPage);

            return await base.SendAsync(request, cancellationToken);
        }
    }
}

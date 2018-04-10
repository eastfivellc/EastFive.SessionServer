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

namespace EastFive.Security.SessionServer.Modules
{
    public class SpaHandlerModule : IHttpModule
    {
        internal const string IndexHTMLFileName = "index.html";

        private Dictionary<string, byte[]> lookupSpaFile;
        static internal byte[] indexHTML;
        private readonly TelemetryClient telemetry;

        public SpaHandlerModule()
        {
            telemetry = new TelemetryClient();
            if (ConfigurationContext.Instance.AppSettings.ContainsKey(Constants.AppSettingKeys.ApplicationInsightsKey))
            {
                var applicationInsightsKey = ConfigurationContext.Instance.AppSettings[Constants.AppSettingKeys.ApplicationInsightsKey];
                telemetry = new TelemetryClient { InstrumentationKey = applicationInsightsKey };
            }
        }

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public void Init(HttpApplication context)
        {
            context.BeginRequest += CheckForAssetMatch;
        }

        private void ExtractSpaFiles(HttpRequest request)
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

                lookupSpaFile = ConfigurationContext.Instance.GetSettingValue(Constants.AppSettingKeys.SpaSiteLocation,
                    (siteLocation) =>
                    {
                        telemetry.TrackEvent($"SpaHandlerModule - ExtractSpaFiles   siteLocation: {siteLocation}");
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
                        telemetry.TrackException(new ArgumentNullException("Could not find SpaSiteLocation - is this key set in app settings?"));
                        return new Dictionary<string, byte[]>();
                    });
            }
        }

        private void CheckForAssetMatch(object sender, EventArgs e)
        {
            var httpApp = (HttpApplication)sender;
            var context = httpApp.Context;
            string filePath = context.Request.FilePath;
            string fileName = VirtualPathUtility.GetFileName(filePath);

            if (lookupSpaFile.IsDefault())
                ExtractSpaFiles(context.Request);

            if (lookupSpaFile.ContainsKey(fileName))
            {
                if (fileName.EndsWith(".js"))
                    context.Response.Headers.Add("content-type", "text/javascript");
                if (fileName.EndsWith(".css"))
                    context.Response.Headers.Add("content-type", "text/css");

                context.Response.BinaryWrite(lookupSpaFile[fileName]);
                HttpContext.Current.ApplicationInstance.CompleteRequest();
                return;
            }

            // TODO: A better job of matching that just grabbing the first segment
            var firstSegments = System.Web.Routing.RouteTable.Routes
                .Where(route => route is System.Web.Routing.Route)
                .Select(route => route as System.Web.Routing.Route)
                .Where(route => !route.Url.IsNullOrWhiteSpace())
                .Select(
                    route => route.Url.Split(new char[] { '/' }).First())
                .ToArray();

            if (firstSegments
                .Where(
                    firstSegment => httpApp.Request.Path.ToLower().StartsWith($"/{firstSegment}"))
                .Any())
            {
                return;
            }


            context.Response.Write(Properties.Resources.indexPage);
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }
    }
}

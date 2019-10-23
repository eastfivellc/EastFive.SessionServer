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
using EastFive.Linq;
using EastFive.Web.Configuration;
using EastFive.Persistence.Azure.StorageTables.Driver;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Linq.Async;

namespace EastFive.Api.Azure.Modules
{
    public class TableInformationAttribute : Attribute, IHandleRoutes
    {
        public const string HeaderKey = "StorageTableInformation";

        public Task<HttpResponseMessage> RouteHandlersAsync(Type controllerType, IApplication httpApp, HttpRequestMessage request, string routeName,
            Func<Task<HttpResponseMessage>> continueExecution)
        {
            return EastFive.Azure.AppSettings.TableInformationToken.ConfigurationString(
                async headerToken =>
                {
                    if (!request.Headers.Contains(HeaderKey))
                        return await continueExecution();

                    if (request.Headers.GetValues(HeaderKey).First() != headerToken)
                        return request.CreateResponse(System.Net.HttpStatusCode.Unauthorized);

                    if(request.Headers.Contains("Migrate"))
                    {
                        var tableData = await controllerType.StorageGetAll().ToArrayAsync();
                        return request.CreateResponse(System.Net.HttpStatusCode.OK, tableData);
                    }

                    var tableInformation = await controllerType.StorageTableInformationAsync();
                    return request.CreateResponse(System.Net.HttpStatusCode.OK, tableInformation);
                },
                why => continueExecution());
        }

    }
}

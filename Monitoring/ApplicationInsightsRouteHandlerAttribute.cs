using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive.Api;
using EastFive.Web.Configuration;
using Microsoft.ApplicationInsights.DataContracts;

namespace EastFive.Azure.Monitoring
{
    public class ApplicationInsightsRouteHandlerAttribute : Attribute, IHandleRoutes, IHandleMethods
    {
        public const string HeaderStatusName = "X-StatusName";
        public const string HeaderStatusInstance = "X-StatusInstance";
        public const string TelemetryStatusName = "StatusName";
        public const string TelemetryStatusInstance = "StatusInstance";

        internal const string HttpRequestMessagePropertyRequestTelemetryKey = "e5_monitoring_requesttelemetry_key";

        public async Task<HttpResponseMessage> RouteHandlersAsync(Type controllerType,
                IApplication httpApp, HttpRequestMessage request, string routeName,
            RouteHandlingDelegate continueExecution)
        {
            var telemetry = new RequestTelemetry()
            {
                Id = Guid.NewGuid().ToString("N"),
                Source = "EastFive.Api",
                Timestamp = DateTimeOffset.UtcNow,
                Url = request.RequestUri,
            };
            var stopwatch = Stopwatch.StartNew();
            request.Properties.Add(HttpRequestMessagePropertyRequestTelemetryKey, telemetry);
            var response = await continueExecution(controllerType, httpApp, request, routeName);

            telemetry.Duration = stopwatch.Elapsed;
            if (telemetry.Properties.ContainsKey(TelemetryStatusName))
                response.Headers.Add(HeaderStatusName, telemetry.Properties[TelemetryStatusName]);
            if (telemetry.Properties.ContainsKey(TelemetryStatusInstance))
                response.Headers.Add(HeaderStatusInstance, telemetry.Properties[TelemetryStatusInstance]);
            var telemetryClient = AppSettings.ApplicationInsights.InstrumentationKey.LoadTelemetryClient();
            telemetryClient.TrackRequest(telemetry);
            return response;
        }
 
        public Task<HttpResponseMessage> RouteHandlersAsync(MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters, 
            IApplication httpApp, HttpRequestMessage request, 
            MethodHandlingDelegate continueExecution)
        {
            var telemetry = request.GetRequestTelemetry();
            telemetry.Name = $"{request.Method} - {method.DeclaringType.FullName}..{method.Name}";
            return continueExecution(method, queryParameters, httpApp, request);
        }
    }

    public static class ApplicationInsightsRouteHandlerExtensions
    {

        public static RequestTelemetry GetRequestTelemetry(this HttpRequestMessage message)
        {
            return (RequestTelemetry)message.Properties[
                ApplicationInsightsRouteHandlerAttribute.HttpRequestMessagePropertyRequestTelemetryKey];
        }
    }
}

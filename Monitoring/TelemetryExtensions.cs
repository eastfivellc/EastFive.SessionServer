using EastFive.Web.Configuration;
using Microsoft.ApplicationInsights;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Monitoring
{
    public static class TelemetryExtensions
    {
        public static TelemetryClient LoadTelemetryClient(this string configKeyName)
        {
            return configKeyName.ConfigurationString(
                instrumentationKey =>
                {
                    var telemetry = new TelemetryClient { InstrumentationKey = instrumentationKey };
                    return telemetry;
                },
                (why) => new TelemetryClient());
        }
    }
}

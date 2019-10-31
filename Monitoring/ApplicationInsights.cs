using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using EastFive.Extensions;
using Microsoft.Azure.ApplicationInsights;
using Microsoft.Rest.Azure.Authentication;

namespace EastFive.Azure.Monitoring
{
    public class ApplicationInsights
    {
        public static async Task<HttpResponseMessage> GetAsync()
        {
            var appId = "{appId}";
            var clientId = "{aadClientAppId}";
            var clientSecret = "{aadAppkey}";

            var domain = "microsoft.onmicrosoft.com";
            var authEndpoint = "https://login.microsoftonline.com";
            var tokenAudience = "https://api.applicationinsights.io/";

            var adSettings = new ActiveDirectoryServiceSettings
            {
                AuthenticationEndpoint = new Uri(authEndpoint),
                TokenAudience = new Uri(tokenAudience),
                ValidateAuthority = true
            };

            // Authenticate with client secret (app key)
            var creds = await ApplicationTokenProvider.LoginSilentAsync(domain, clientId, clientSecret, adSettings);

            // New up a client with credentials and AI application Id
            var client = new ApplicationInsightsDataClient(creds);
            client.AppId = appId;

            return new HttpResponseMessage();
        }
    }
}
using BlackBarLabs.Extensions;
using EastFive.Api;
using EastFive.Api.Azure;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Functions
{
    public class InvokeFunction : InvokeApplication
    {
        public override IApplication Application => azureApplication;

        private AzureApplication azureApplication;

        public InvokeFunction(AzureApplication application, Uri serverUrl, string apiRouteName)
            : base(serverUrl, apiRouteName)
        {
            this.azureApplication = application;
        }

        public override Task<HttpResponseMessage> SendAsync<TResource>(RequestMessage<TResource> requestMessage, 
            HttpRequestMessage httpRequest)
        {
            return InvocationMessage.SendAsync(this.azureApplication, requestMessage, httpRequest);
        }
    }
}

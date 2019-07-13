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

        public override async Task<HttpResponseMessage> SendAsync<TResource>(RequestMessage<TResource> requestMessage, 
            HttpRequestMessage httpRequest)
        {
            var invocationMessageRef = Ref<InvocationMessage>.SecureRef();
            var invocationMessage = new InvocationMessage
            {
                invocationRef = invocationMessageRef,
                headers = httpRequest.Headers
                    .Select(hdr => hdr.Key.PairWithValue(hdr.Value.First()))
                    .ToDictionary(),
                requestUri = httpRequest.RequestUri,
                content = httpRequest.Content.IsDefaultOrNull() ?
                    default(byte[])
                    :
                    await httpRequest.Content.ReadAsByteArrayAsync(),
            };
            return await await invocationMessage.StorageCreateAsync(
                async (created) =>
                {
                    var byteContent = invocationMessageRef.id.ToByteArray();
                    
                    return await EastFive.Web.Configuration.Settings.GetString(
                        AppSettings.FunctionProcessorQueueTriggerName,
                        async (queueTriggerName) =>
                        {
                            await azureApplication.SendQueueMessageAsync(queueTriggerName, byteContent);
                            var invocationSerialized = JsonConvert.SerializeObject(invocationMessage,
                                new EastFive.Api.Serialization.Converter());
                            var response = new HttpResponseMessage(System.Net.HttpStatusCode.Accepted)
                            {
                                RequestMessage = httpRequest,
                                ReasonPhrase = "Send to background-task message queue",
                                Content = new StringContent(
                                    invocationSerialized, Encoding.UTF8,
                                    "x-application/eastfive-invocationmessage"),
                            };
                            return response;
                        },
                        (why) => throw new Exception(why));
                },
                () => throw new Exception());

            //var jsonRequest = JsonConvert.SerializeObject(requestMessage);
            //var bytes = jsonRequest.GetBytes(Encoding.UTF8);
            //await azureApplication.SendQueueMessageAsync("background-task", bytes);
            //var response = new HttpResponseMessage(System.Net.HttpStatusCode.Accepted)
            //{
            //    RequestMessage = httpRequest,
            //    ReasonPhrase = "Send to background-task message queue",
            //};
            //return response;
        }
    }
}

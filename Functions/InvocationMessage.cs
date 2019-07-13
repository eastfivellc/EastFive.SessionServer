using EastFive;
using EastFive.Extensions;
using EastFive.Api;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace EastFive.Azure.Functions
{
    [DataContract]
    [StorageTable]
    public struct InvocationMessage : IReferenceable
    {
        [JsonIgnore]
        public Guid id => this.invocationRef.id;

        public const string IdPropertyName = "id";
        [JsonProperty(PropertyName = IdPropertyName)]
        [ApiProperty(PropertyName = IdPropertyName)]
        [RowKey]
        [StandardParititionKey]
        public IRef<InvocationMessage> invocationRef;

        [JsonProperty]
        [Storage]
        public Uri requestUri;

        [JsonProperty]
        [Storage]
        public string method;

        [JsonProperty]
        [Storage]
        public IDictionary<string, string> headers;

        [JsonProperty]
        [Storage]
        public byte[] content;

        public static Task<HttpResponseMessage> InvokeAsync(byte [] invocationMessageIdBytes,
            IApplication application,
            InvokeApplicationDirect invokeApplication)
        {
            var invocationMessageId = new Guid(invocationMessageIdBytes);
            return InvokeAsync(invocationMessageId, application, invokeApplication);
        }

        public static async Task<HttpResponseMessage> InvokeAsync(Guid invocationMessageId, 
            IApplication application,
            InvokeApplicationDirect invokeApplication)
        {
            var invocationMessageRef = invocationMessageId.AsRef<InvocationMessage>();
            return await await invocationMessageRef.StorageGetAsync(
                invocationMessage =>
                {
                    if (invocationMessage.method.IsNullOrEmpty())
                    {
                        //throw new Exception("Invalid invocation message: no method");
                        invocationMessage.method = "patch";
                    }

                    var httpRequest = new HttpRequestMessage(
                        new HttpMethod(invocationMessage.method),
                        invocationMessage.requestUri);
                    var config = new HttpConfiguration();
                    httpRequest.SetConfiguration(config);

                    foreach (var headerKVP in invocationMessage.headers)
                        httpRequest.Headers.Add(headerKVP.Key, headerKVP.Value);

                    if (!invocationMessage.content.IsDefaultOrNull())
                    {
                        httpRequest.Content = new ByteArrayContent(invocationMessage.content);
                        httpRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    }
                    var requestMessage = new RequestMessage<object>(invokeApplication, httpRequest);
                    return invokeApplication.SendAsync(requestMessage, httpRequest);
                },
                ResourceNotFoundException.StorageGetAsync<Task<HttpResponseMessage>>);
        }
    }
}

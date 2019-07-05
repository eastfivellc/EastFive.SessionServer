using EastFive.Api;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Functions
{
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

        [Storage]
        public Uri requestUri;

        [Storage]
        public string method;

        [Storage]
        public IDictionary<string, string> headers;

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
                    var request = new HttpRequestMessage(
                        new HttpMethod(invocationMessage.method),
                        invocationMessage.requestUri);
                    var requestMessage = new RequestMessage<object>(invokeApplication, request);
                    return invokeApplication.SendAsync(requestMessage, request);
                },
                ResourceNotFoundException.StorageGetAsync<Task<HttpResponseMessage>>);
        }
    }
}

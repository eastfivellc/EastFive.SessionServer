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
using EastFive.Api.Controllers;
using EastFive.Api.Azure;
using BlackBarLabs.Extensions;
using EastFive.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Queue;
using EastFive.Linq.Async;
using EastFive.Linq;

namespace EastFive.Azure.Functions
{
    [FunctionViewController6(
        Route = "InvocationMessage",
        Resource = typeof(InvocationMessage),
        ContentType = "x-application/eastfive.azure.invocation-message",
        ContentTypeVersion = "0.1")]
    [DataContract]
    [StorageTable]
    public struct InvocationMessage : IReferenceable
    {
        #region Properties

        [JsonIgnore]
        public Guid id => this.invocationRef.id;

        public const string IdPropertyName = "id";
        [JsonProperty(PropertyName = IdPropertyName)]
        [ApiProperty(PropertyName = IdPropertyName)]
        [RowKey]
        [StandardParititionKey]
        public IRef<InvocationMessage> invocationRef;

        [LastModified]
        public DateTimeOffset lastModified;

        [StorageLookup]
        public string LastUsedDay
        {
            get
            {
                var epoch = new DateTime(2019, 1, 1);
                var span = lastModified - epoch;
                var totalDays = (int)span.TotalDays;
                return totalDays.ToString();
            }
            set
            {
                // ignore
            }
        }

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

        public const string LastExecutedPropertyName = "last_executed";
        [JsonProperty(PropertyName = LastExecutedPropertyName)]
        [ApiProperty(PropertyName = LastExecutedPropertyName)]
        [Storage]
        public DateTime? lastExecuted;
        
        #endregion

        #region Http Methods

        [HttpAction("Invoke")]
        public static async Task<HttpResponseMessage> RunAsync(
                [UpdateId]IRefs<InvocationMessage> invocationMessageRefs,
                [HeaderLog]EastFive.Analytics.ILogger analyticsLog,
                InvokeApplicationDirect invokeApplication,
                MultipartResponseAsync onRun)
        {
            var messages = await invocationMessageRefs.refs
                .Select(invocationMessageRef => InvokeAsync(invocationMessageRef, invokeApplication))
                .AsyncEnumerable()
                .ToArrayAsync();
            return await onRun(messages);
        }

        #endregion

        public static IEnumerableAsync<HttpResponseMessage> InvokeAsync(
                byte [] invocationMessageIdsBytes,
            IApplication application,
            IInvokeApplication invokeApplication)
        {
            return invocationMessageIdsBytes
                .Split(index => 16)
                .Select(
                    invocationMessageIdBytes =>
                    {
                        var idBytes = invocationMessageIdBytes.ToArray();
                        var invocationMessageId = new Guid(idBytes);
                        var invocationMessageRef = invocationMessageId.AsRef<InvocationMessage>();
                        return InvokeAsync(invocationMessageRef, invokeApplication);
                    })
                .Parallel();
        }

        internal static async Task<HttpResponseMessage> CreateAsync(
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
                method = httpRequest.Method.Method,
            };
            return await invocationMessage.StorageCreateAsync(
                (created) =>
                {
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
                () => throw new Exception());
        }

        public static Task<CloudQueueMessage> SendToQueueAsync(IRef<InvocationMessage> invocationMessageRef,
            AzureApplication azureApplication)
        {
            var byteContent = invocationMessageRef.id.ToByteArray();
            return EastFive.Web.Configuration.Settings.GetString(
                AppSettings.FunctionProcessorQueueTriggerName,
                (queueTriggerName) =>
                {
                    return azureApplication.SendQueueMessageAsync(queueTriggerName, byteContent);
                },
                (why) => throw new Exception(why));
        }

        public static Task<CloudQueueMessage> SendToQueueAsync(IRefs<InvocationMessage> invocationMessageRef,
            AzureApplication azureApplication)
        {
            var byteContent = invocationMessageRef.ids.Select(id => id.ToByteArray()).SelectMany().ToArray();
            return EastFive.Web.Configuration.Settings.GetString(
                AppSettings.FunctionProcessorQueueTriggerName,
                (queueTriggerName) =>
                {
                    return azureApplication.SendQueueMessageAsync(queueTriggerName, byteContent);
                },
                (why) => throw new Exception(why));
        }

        public static Task<HttpResponseMessage> InvokeAsync(IRef<InvocationMessage> invocationMessageRef,
            IInvokeApplication invokeApplication)
        {
            return invocationMessageRef.StorageUpdateAsync(
                async (invocationMessage, saveAsync) =>
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
                    invocationMessage.lastExecuted = DateTime.UtcNow;
                    return await invokeApplication.SendAsync(requestMessage, httpRequest);
                },
                ResourceNotFoundException.StorageGetAsync<HttpResponseMessage>);
        }
    }
}

using BlackBarLabs.Extensions;
using EastFive.Api;
using EastFive.Api.Azure;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Functions
{
    public static class ResourceQueryCompilationExtensions
    {
        public static async Task<InvocationMessage> FunctionAsync<TResource>(this IQueryable<TResource> urlQuery,
            HttpMethod httpMethod = default,
            IInvokeApplication applicationInvoker = default)
        {
            var request = urlQuery.Request(httpMethod: httpMethod, applicationInvoker: applicationInvoker);
            var invocationMessageRef = Ref<InvocationMessage>.NewRef();
            var invocationMessage = new InvocationMessage
            {
                invocationRef = invocationMessageRef,
                headers = request.Headers
                    .Select(hdr => hdr.Key.PairWithValue(hdr.Value.First()))
                    .ToDictionary(),
                requestUri = request.RequestUri,
                content = request.Content.IsDefaultOrNull() ?
                    default(byte[])
                    :
                    await request.Content.ReadAsByteArrayAsync(),
            };
            return await await invocationMessage.StorageCreateAsync(
                async (created) =>
                {
                    var byteContent = invocationMessageRef.id.ToByteArray();
                    AzureApplication GetApplication()
                    {
                        if (urlQuery is RequestMessage<TResource>)
                        {
                            var requestQuery = urlQuery as RequestMessage<TResource>;
                            return requestQuery.Application as AzureApplication;
                        }
                        var app = new AzureApplication();
                        app.ApplicationStart();
                        return app;
                    }
                    var application = GetApplication();
                    return await EastFive.Web.Configuration.Settings.GetString(
                        AppSettings.FunctionProcessorQueueTriggerNamePercent,
                        async (queueTriggerName) =>
                        {
                            await application.SendQueueMessageAsync(queueTriggerName, byteContent);
                            return invocationMessage;
                        },
                        (why) => throw new Exception(why));
                },
                () => throw new Exception());
        }
    }
}

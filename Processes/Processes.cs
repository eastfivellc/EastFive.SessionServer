using BlackBarLabs.Extensions;
using EastFive.Api.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using EastFive.Linq;
using EastFive.Collections.Generic;

namespace EastFive.Azure
{
    public struct Process
    {
        public Guid processId;

        public Guid processStageId;
        public DateTime createdOn;

        public Guid resourceId;
        public Type resourceType;

        public Guid? previousStep;
        public Guid? confirmedBy;
        public DateTime? confirmedWhen;
        public DateTime? invalidatedWhen;

        public struct ProcessStageResource
        {
            public Guid? resourceId;
            public string key;
            public Type type;
        }
        public ProcessStageResource[] resources;
    }

    public static class Processes
    {
        public static async Task<TResult> CreateAsync<TResult>(Guid processId,
                Guid processStageId, Guid resourceId, DateTime createdOn,
                KeyValuePair<string, Guid>[] resourceIds,
                Guid? confirmedNext, DateTime? confirmedWhen, Guid? confirmedBy,
                EastFive.Api.Controllers.Security security,
            Func<TResult> onCreated,
            Func<TResult> onAlreadyExists,
            Func<TResult> onStageDoesNotExist,
            Func<string, TResult> onFailure)
        {
            return await await Persistence.ProcessStageDocument.FindByIdAsync(processStageId,
                async stage =>
                {
                    return await await ProcessStageTypes.FindIdAsync(stage.processStageTypeId,
                        async (stageType) =>
                        {
                            var resourceType = stageType.resourceType;

                            return await stageType.resourceKeys
                                .FlatMap(resourceIds.ToDictionary(),
                                    async (kvp, unusedResourceKvps, next, skip, tail) =>
                                    {
                                        if (!unusedResourceKvps.ContainsKey(kvp.Key))
                                        {
                                            if (confirmedWhen.HasValue)
                                                return await tail(onFailure($"Missing resource for `{kvp.Key}`").ToTask());
                                            return await skip(unusedResourceKvps);
                                        }
                                        return await next(
                                            new Process.ProcessStageResource()
                                            {
                                                key = kvp.Key,
                                                resourceId = unusedResourceKvps[kvp.Key],
                                                type = kvp.Value,
                                            },
                                            unusedResourceKvps
                                                .Where(kvpR => kvpR.Key != kvp.Key)
                                                .ToDictionary());
                                    },
                                    async (Process.ProcessStageResource[] procStageResources, Dictionary<string, Guid> unusedResourceIds) =>
                                    {
                                        if (unusedResourceIds.Any())
                                            return onFailure($"`{unusedResourceIds.First().Key}` is not valid resource key for a stage of type `{stageType.processStageTypeId}`");

                                        // TODO: If confirmed is set, ensure that the security actor posesses a position that is authorized to move the process forward
                                        return await Persistence.ProcessDocument.CreateAsync(processId,
                                                        processStageId, stage.ownerId,
                                                        resourceId, resourceType, createdOn,
                                                        procStageResources,
                                                        confirmedNext, confirmedWhen, confirmedBy,
                                                    onCreated,
                                                    onAlreadyExists);
                                    });
                        },
                        onStageDoesNotExist.AsAsyncFunc());
                },
                onStageDoesNotExist.AsAsyncFunc());
        }

        public static Task<TResult> FindByIdAsync<TResult>(Guid processStageId, EastFive.Api.Controllers.Security security,
            Func<Process, TResult> onFound,
            Func<TResult> onNotFound,
            Func<TResult> onUnauthorized)
        {
            return Persistence.ProcessDocument.FindByIdAsync(processStageId,
                (processStage) =>
                {
                    return onFound(processStage);
                },
                onNotFound);
        }

        public static Task<TResult> FindByResourceAsync<TResult>(Guid resourceId, Type resourceType,
                EastFive.Api.Controllers.Security security,
            Func<Process[], TResult> onFound,
            Func<TResult> onResourceNotFound,
            Func<TResult> onUnauthorized)
        {
            return Persistence.ProcessDocument.FindByResourceAsync(resourceId, resourceType,
                (processStages) =>
                {
                    return onFound(processStages);
                },
                onResourceNotFound);
        }

        public static Task<TResult> DeleteByIdAsync<TResult>(Guid processStageId, EastFive.Api.Controllers.Security security,
            Func<TResult> onDeleted,
            Func<TResult> onNotFound,
            Func<TResult> onUnauthorized)
        {
            return Persistence.ProcessDocument.DeleteByIdAsync(processStageId,
                () =>
                {
                    return onDeleted();
                },
                onNotFound);
        }
    }
}

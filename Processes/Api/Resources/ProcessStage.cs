﻿using BlackBarLabs.Api;
using BlackBarLabs.Api.Resources;
using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using EastFive.Api;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http.Routing;
using EastFive.Api.Controllers;
using BlackBarLabs.Extensions;
using System.Linq;
using EastFive.Security.SessionServer;
using EastFive.Extensions;
using System.Collections.Generic;

namespace EastFive.Api.Azure.Resources
{
    [DataContract]
    [FunctionViewController(Route = "ProcessStage")]
    public class ProcessStage : ResourceBase
    {
        #region Properties

        public class ConfirmableResource
        {
            public const string ProcessStageNextPropertyName = "process_stage_next";
            [JsonProperty(PropertyName = ProcessStageNextPropertyName)]
            public WebId ProcessStageNext { get; set; }

            public const string PositionsPropertyName = "positions";
            [JsonProperty(PropertyName = PositionsPropertyName)]
            public WebId [] Positions { get; set; }
        }

        public const string OwnerPropertyName = "owner";
        [JsonProperty(PropertyName = OwnerPropertyName)]
        public WebId Owner { get; set; }

        public const string TypePropertyName = "type";
        [JsonProperty(PropertyName = TypePropertyName)]
        public WebId Type { get; set; }

        public const string TitlePropertyName = "title";
        [JsonProperty(PropertyName = TitlePropertyName)]
        public string Title { get; set; }
        
        public const string ConfirmablePropertyName = "confirmable";
        [JsonProperty(PropertyName = ConfirmablePropertyName)]
        public ConfirmableResource[] Confirmable { get; set; }

        public const string EditablePropertyName = "editable";
        [JsonProperty(PropertyName = EditablePropertyName)]
        public WebId[] Editable { get; set; }

        public const string CompletablePropertyName = "completable";
        [JsonProperty(PropertyName = CompletablePropertyName)]
        public WebId[] Completable { get; set; }

        public const string ViewablePropertyName = "viewable";
        [JsonProperty(PropertyName = ViewablePropertyName)]
        public WebId[] Viewable { get; set; }
        
        #endregion

        #region Methods

        #region GET

        [EastFive.Api.HttpGet]
        public static Task<HttpResponseMessage> FindByIdAsync(
                [QueryParameter(CheckFileName = true)]Guid id,
                EastFive.Api.Security security, UrlHelper url,
            ContentResponse onFound,
            NotFoundResponse onNotFound,
            UnauthorizedResponse onUnauthorized)
        {
            return EastFive.Azure.ProcessStages.FindByIdAsync(id, security,
                (processStage) =>
                    onFound(GetResource(processStage, url)),
                () => onNotFound(),
                () => onUnauthorized());
        }

        [EastFive.Api.HttpGet]
        public static async Task<HttpResponseMessage> FindByResourceAsync(
                [QueryParameter]Guid resourceId,
                EastFive.Api.Security security, UrlHelper url,
            MultipartAcceptArrayResponseAsync onMultipart,
            ReferencedDocumentNotFoundResponse onResourceNotFound,
            UnauthorizedResponse onUnauthorized)
        {
            return await await EastFive.Azure.ProcessStages.FindByResourceAsync(resourceId, security,
                (processStages) => onMultipart(processStages.Select(ps => GetResource(ps, url))),
                () => onResourceNotFound().ToTask(),
                () => onUnauthorized().ToTask());
        }

        [EastFive.Api.HttpGet]
        public static async Task<HttpResponseMessage> FindByFirstStepByActorAndTypeAsync(
                [QueryParameter(Name = Resources.ProcessStage.OwnerPropertyName)]Guid ownerId,
                [QueryParameter(Name = Resources.ProcessStage.TypePropertyName)]Type resourceType,
                [QueryParameter(Name = "processstage." + Resources.ProcessStage.ConfirmablePropertyName + "." + Resources.ProcessStage.ConfirmableResource.ProcessStageNextPropertyName)]
                    EastFive.Api.Controllers.WebIdNone nextStage,
                AzureApplication application, EastFive.Api.Security security, UrlHelper url,
            MultipartAcceptArrayResponseAsync onMultipart,
            ReferencedDocumentNotFoundResponse onResourceNotFound,
            UnauthorizedResponse onUnauthorized)
        {
            return await await EastFive.Azure.ProcessStages.FindStartByActorAndResourceTypeAsync(ownerId, resourceType,
                    security,
                (processStages) => onMultipart(processStages.Select(ps => GetResource(ps, url))),
                () => onResourceNotFound().ToTask(),
                () => onUnauthorized().ToTask());
        }

        internal static Resources.ProcessStage GetResource(EastFive.Azure.ProcessStage processStage, UrlHelper url)
        {
            return new Resources.ProcessStage
            {
                Id = url.GetWebId<ProcessStage>(processStage.processStageId),
                Owner = Library.configurationManager.GetActorLink(processStage.ownerId, url),
                Title = processStage.title,
                Type = url.GetWebId<ProcessStageType>(processStage.processStageTypeId),
                Confirmable = processStage.confirmableIds
                    .Select(
                        confirmableKvp => new Resources.ProcessStage.ConfirmableResource
                        {
                            Positions = confirmableKvp.Key
                                .Select(actorId => Library.configurationManager.GetActorLink(actorId, url))
                                .ToArray(),
                            ProcessStageNext = url.GetWebId<ProcessStage>(confirmableKvp.Value),
                        })
                    .ToArray(),
                Editable = processStage.editableIds
                    .Select(actorId => Library.configurationManager.GetActorLink(actorId, url))
                    .ToArray(),
                Completable = processStage.completableIds
                    .Select(actorId => Library.configurationManager.GetActorLink(actorId, url))
                    .ToArray(),
                Viewable = processStage.viewableIds
                    .Select(actorId => Library.configurationManager.GetActorLink(actorId, url))
                    .ToArray(),
            };
        }

        #endregion

        [EastFive.Api.HttpPost(Type = typeof(Resources.ProcessStage), MatchAllBodyParameters = false)]
        public static Task<HttpResponseMessage> CreateAsync(
                [Property(Name = Resources.ProcessStage.IdPropertyName)]
                    Guid processStageId,
                [Property(Name = Resources.ProcessStage.OwnerPropertyName)]
                    Guid ownerId,
                [Property(Name = Resources.ProcessStage.TypePropertyName)]
                    Guid processStageTypeId,
                [PropertyOptional(Name = Resources.ProcessStage.TitlePropertyName)]
                    string title,
                [PropertyOptional(Name = Resources.ProcessStage.ViewablePropertyName)]
                    Guid [] viewableIds,
                [PropertyOptional(Name = Resources.ProcessStage.CompletablePropertyName)]
                    Guid [] completableIds,
                [PropertyOptional(Name = Resources.ProcessStage.EditablePropertyName)]
                    Guid [] editableIds,
                [PropertyOptional(Name = Resources.ProcessStage.ConfirmablePropertyName)]
                    Resources.ProcessStage.ConfirmableResource [] confirmables,
                EastFive.Api.Security security, EastFive.Api.Azure.AzureApplication application,
            CreatedResponse onCreated,
            AlreadyExistsResponse onAlreadyExists,
            ReferencedDocumentDoesNotExistsResponse<Resources.ProcessStageType> onTypeDoesNotExist,
            ReferencedDocumentDoesNotExistsResponse<Resources.ProcessStage> onConfirmationStageDoesNotExist,
            ReferencedDocumentDoesNotExistsResponse<BlackBarLabs.Api.ResourceBase> onActorDoesNotExists,
            UnauthorizedResponse onUnauthorized,
            GeneralConflictResponse onFailure)
        {
            return EastFive.Azure.ProcessStages.CreateAsync(processStageId, ownerId, processStageTypeId, title,
                    viewableIds, editableIds, completableIds,
                    confirmables
                        .Select(
                            confirmable => 
                                confirmable.ProcessStageNext.ToGuid().Value
                                    .PairWithKey(
                                        confirmable.Positions
                                            .Select(position => position.ToGuid().Value)
                                            .ToArray()))
                        .ToArray(),
                    security,
                () => onCreated(),
                () => onAlreadyExists(),
                () => onTypeDoesNotExist(),
                (missingStageId) => onConfirmationStageDoesNotExist(),
                () => onActorDoesNotExists(),
                (why) => onFailure(why));
        }

        [EastFive.Api.HttpPut(Type = typeof(Resources.ProcessStage), MatchAllBodyParameters = false)]
        public static Task<HttpResponseMessage> UpdateConnectorAsync(
                [Property(Name = Resources.ProcessStage.IdPropertyName)]
                    Guid processStageId,
                [PropertyOptional(Name = Resources.ProcessStage.TypePropertyName)]
                    Guid? processStageTypeId,
                [PropertyOptional(Name = Resources.ProcessStage.TitlePropertyName)]
                    string title,
                [PropertyOptional(Name = Resources.ProcessStage.ViewablePropertyName)]
                    Guid [] viewableIds,
                [PropertyOptional(Name = Resources.ProcessStage.CompletablePropertyName)]
                    Guid [] completableIds,
                [PropertyOptional(Name = Resources.ProcessStage.EditablePropertyName)]
                    Guid [] editableIds,
                [PropertyOptional(Name = Resources.ProcessStage.ConfirmablePropertyName)]
                    Resources.ProcessStage.ConfirmableResource [] confirmables,
                EastFive.Api.Security security, Context context, HttpRequestMessage request, UrlHelper url,
            NoContentResponse onUpdated,
            NotFoundResponse onNotFound,
            UnauthorizedResponse onUnauthorized,
            GeneralConflictResponse onFailure)
        {
            return EastFive.Azure.ProcessStages.UpdateAsync(processStageId,
                    processStageTypeId, title,
                    viewableIds, completableIds, editableIds,
                    confirmables.IsDefaultOrNull() ?
                        default(KeyValuePair<Guid[], Guid>[])
                        :
                        confirmables
                            .Select(
                                confirmable => confirmable.ProcessStageNext.ToGuid()
                                    .PairWithValue(confirmable.Positions.Select(pos => pos.ToGuid()).ToArray()))
                            .Where(kvp => kvp.Key.HasValue)
                            .Select(kvp => kvp.Key.Value.PairWithKey(
                                kvp.Value.Where(v => v.HasValue).Select(v => v.Value).ToArray()))
                            .ToArray(),
                    security,
                () => onUpdated(),
                () => onNotFound(),
                () => onUnauthorized(),
                (why) => onFailure(why));
        }

        [EastFive.Api.HttpOptions(MatchAllBodyParameters = false)]
        public static HttpResponseMessage Options(HttpRequestMessage request, UrlHelper url,
            ContentResponse onOption)
        {
            return onOption(GetResource(
                new EastFive.Azure.ProcessStage()
                {
                    processStageId = Guid.NewGuid(),
                    processStageTypeId = Guid.NewGuid(),
                    confirmableIds = Enumerable.Range(0, 2)
                        .Select(
                            i => Enumerable.Range(0, 3)
                                .Select(j => Guid.NewGuid()).ToArray()
                                    .PairWithValue(Guid.NewGuid()))
                        .ToArray(),
                    editableIds = Enumerable.Range(0, 2).Select(i => Guid.NewGuid()).ToArray(),
                    completableIds = Enumerable.Range(0, 2).Select(i => Guid.NewGuid()).ToArray(),
                    viewableIds = Enumerable.Range(0, 2).Select(i => Guid.NewGuid()).ToArray(),
                }, url));
        }
        
        #endregion
    }
}
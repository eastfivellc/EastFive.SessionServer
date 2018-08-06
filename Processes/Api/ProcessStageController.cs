using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http.Routing;
using System.Threading.Tasks;
using System.Linq.Expressions;

using EastFive.Collections.Generic;
using EastFive;
using EastFive.Api.Controllers;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Api;
using EastFive.Azure.Synchronization;
using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using EastFive.Security.SessionServer;

namespace EastFive.Azure.Api.Controllers
{
    [FunctionViewController(Route = "ProcessStage")]
    public class ProcessStageController
    {

        #region GET

        [EastFive.Api.HttpGet]
        public static Task<HttpResponseMessage> FindByIdAsync([QueryDefaultParameter][Required]Guid id,
                EastFive.Api.Controllers.Security security, UrlHelper url,
            ContentResponse onFound,
            NotFoundResponse onNotFound,
            UnauthorizedResponse onUnauthorized)
        {
            return ProcessStages.FindByIdAsync(id, security,
                (processStage) =>
                    onFound(GetResource(processStage, url)),
                () => onNotFound(),
                () => onUnauthorized());
        }
        
        [EastFive.Api.HttpGet]
        public static async Task<HttpResponseMessage> FindByResourceAsync(
                [Required]Guid resourceId,
                EastFive.Api.Controllers.Security security,UrlHelper url,
            MultipartAcceptArrayResponseAsync onMultipart,
            ReferencedDocumentNotFoundResponse onResourceNotFound,
            UnauthorizedResponse onUnauthorized)
        {
            return await await ProcessStages.FindByResourceAsync(resourceId, security,
                (processStages) => onMultipart(processStages.Select(ps => GetResource(ps, url))),
                () => onResourceNotFound().ToTask(),
                () => onUnauthorized().ToTask());
        }

        [EastFive.Api.HttpGet]
        public static async Task<HttpResponseMessage> FindByFirstStepByActorAndTypeAsync(
                [Required(Name = Resources.ProcessStage.OwnerPropertyName)]Guid ownerId,
                [Required(Name = Resources.ProcessStage.TypePropertyName)]Type resourceType,
                [Required(Name = "processstage." + Resources.ProcessStage.ConfirmablePropertyName + "." + Resources.ProcessStage.ConfirmableResource.ProcessStageNextPropertyName)]
                    EastFive.Api.Controllers.WebIdNone nextStage,
                Application application, EastFive.Api.Controllers.Security security, UrlHelper url,
            MultipartAcceptArrayResponseAsync onMultipart,
            ReferencedDocumentNotFoundResponse onResourceNotFound,
            UnauthorizedResponse onUnauthorized)
        {
            return await await ProcessStages.FindStartByActorAndResourceTypeAsync(ownerId, resourceType,
                    security,
                (processStages) => onMultipart(processStages.Select(ps => GetResource(ps, url))),
                () => onResourceNotFound().ToTask(),
                () => onUnauthorized().ToTask());
        }

        internal static Resources.ProcessStage GetResource(ProcessStage processStage, UrlHelper url)
        {
            return new Resources.ProcessStage
            {
                Id = url.GetWebId<ProcessStageController>(processStage.processStageId),
                Owner = Library.configurationManager.GetActorLink(processStage.ownerId, url),
                Title = processStage.title,
                Type = url.GetWebId<ProcessStageTypeController>(processStage.processStageTypeId),
                Confirmable = processStage.confirmableIds
                    .Select(
                        confirmableKvp => new Resources.ProcessStage.ConfirmableResource
                        {
                            Positions = confirmableKvp.Key
                                .Select(actorId => Library.configurationManager.GetActorLink(actorId, url))
                                .ToArray(),
                            ProcessStageNext = url.GetWebId<ProcessStageController>(confirmableKvp.Value),
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
        public static Task<HttpResponseMessage> CreateConnectorAsync(
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
                EastFive.Api.Controllers.Security security, Context context, HttpRequestMessage request, UrlHelper url,
            CreatedResponse onCreated,
            CreatedBodyResponse onCreatedAndModified,
            AlreadyExistsResponse onAlreadyExists,
            AlreadyExistsReferencedResponse onRelationshipAlreadyExists,
            ReferencedDocumentNotFoundResponse onReferenceNotFound,
            UnauthorizedResponse onUnauthorized,
            GeneralConflictResponse onFailure)
        {
            throw new NotImplementedException();
            //return Connectors.CreateConnectorAsync(id, source, destination,
            //        Flow, destinationIntegration, security.performingAsActorId, security.claims,
            //    () => onCreated(),
            //    (connection) =>
            //    {
            //        return request.Headers.Accept
            //            .OrderByDescending(accept => accept.Quality.HasValue ? accept.Quality.Value : 1.0)
            //            .First(
            //                (accept, next) =>
            //                {
            //                    if(
            //                        accept.MediaType.ToLower() == "x-ordering/connection" || 
            //                        accept.MediaType.ToLower() == "x-ordering/connection+json")
            //                        return onCreatedAndModified(
            //                            ConnectionController.GetResource(connection, url),
            //                            "x-ordering/connection+json");

            //                    if (
            //                        accept.MediaType.ToLower() == "x-ordering/connector" ||
            //                        accept.MediaType.ToLower() == "x-ordering/connector+json" ||
            //                        accept.MediaType.ToLower() == "application/json")
            //                        return onCreatedAndModified(
            //                            GetResource(connection.connector, connection.adapterExternal.integrationId, url),
            //                            "x-ordering/connector+json");
                                
            //                    return next();
            //                },
            //                () => onCreatedAndModified(GetResource(connection.connector, connection.adapterExternal.integrationId, url)));
            //    },
            //    () => onAlreadyExists(),
            //    (existingConnectorId) => onRelationshipAlreadyExists(existingConnectorId),
            //    (brokenId) => onReferenceNotFound(),
            //    (why) => onFailure(why));
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
                EastFive.Api.Controllers.Security security, Context context, HttpRequestMessage request, UrlHelper url,
            NoContentResponse onUpdated,
            NotFoundResponse onNotFound,
            UnauthorizedResponse onUnauthorized,
            GeneralConflictResponse onFailure)
        {
            return ProcessStages.UpdateAsync(processStageId,
                    processStageTypeId, title,
                    viewableIds, completableIds, editableIds,
                    confirmables.IsDefaultOrNull()?
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
                new ProcessStage()
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

    }
}

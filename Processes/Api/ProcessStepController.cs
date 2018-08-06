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
using BlackBarLabs.Api.Resources;

namespace EastFive.Azure.Api.Controllers
{
    [FunctionViewController(Route = "ProcessStep")]
    public class ProcessStepController
    {

        #region GET

        [EastFive.Api.HttpGet]
        public static Task<HttpResponseMessage> FindByIdAsync(
                [QueryDefaultParameter][Required(Name = Resources.ProcessStep.IdPropertyName)]Guid id,
                Application httpApplication, EastFive.Api.Controllers.Security security, UrlHelper url,
            ContentResponse onFound,
            NotFoundResponse onNotFound,
            UnauthorizedResponse onUnauthorized)
        {
            return Processes.FindByIdAsync(id, security,
                (process) =>
                    onFound(GetResource(process, httpApplication, url)),
                () => onNotFound(),
                () => onUnauthorized());
        }

        internal static Resources.ProcessStep GetResource(Process process, Application httpApplication, UrlHelper urlHelper)
        {
            return new Resources.ProcessStep
            {
                Id = urlHelper.GetWebId<ProcessStepController>(process.processId),

                Stage = urlHelper.GetWebId<ProcessStageController>(process.processStageId),

                Resource = httpApplication.GetResourceLink(process.resourceType, process.resourceId, urlHelper),
                CreatedOn = process.createdOn,

                ConfirmedBy = process.confirmedBy.HasValue?
                    Security.SessionServer.Library.configurationManager.GetActorLink(process.confirmedBy.Value, urlHelper)
                    :
                    default(WebId),
                ConfirmedWhen = process.confirmedWhen,
                ConfirmedNext = urlHelper.GetWebId<ProcessStepController>(process.previousStep),
                Resources = process.resources
                    .Select(resource => httpApplication.GetResourceLink(process.resourceType, resource.resourceId, urlHelper))
                    .ToArray(),
                ResourceKeys = process.resources
                    .Select(resource => (resource.key))
                    .ToArray(),
            };
        }

        #endregion

        [EastFive.Api.HttpPost(Type = typeof(Api.Resources.ProcessStep), MatchAllBodyParameters = false)]
        public static Task<HttpResponseMessage> CreateAsync(
                [Property(Name = Resources.ProcessStep.IdPropertyName)]Guid processId,
                [Property(Name = Resources.ProcessStep.ResourcePropertyName)]Guid resourceId,
                [Property(Name = Resources.ProcessStep.StagePropertyName)]Guid processStageId,
                [Property(Name = Resources.ProcessStep.CreatedOnPropertyName)]DateTime createdOn,
                [PropertyOptional(Name = Resources.ProcessStep.ConfirmedNextPropertyName)]Guid? confirmedNextId,
                [PropertyOptional(Name = Resources.ProcessStep.ConfirmedByPropertyName)]Guid? confirmedById,
                [PropertyOptional(Name = Resources.ProcessStep.ConfirmedWhenPropertyName)]DateTime? confirmedWhen,
                [PropertyOptional(Name = Resources.ProcessStep.ResourceKeysPropertyName)]string [] resourceKeys,
                [PropertyOptional(Name = Resources.ProcessStep.ResourcesPropertyName)]Guid[] resources,
                EastFive.Api.Controllers.Security security, UrlHelper url,
            CreatedResponse onCreated,
            AlreadyExistsResponse onAlreadyExists,
            ReferencedDocumentNotFoundResponse onStageNotFound,
            UnauthorizedResponse onUnauthorized,
            GeneralConflictResponse onFailure)
        {
            return Processes.CreateAsync(processId, processStageId, resourceId, createdOn,
                    resourceKeys.NullToEmpty().Zip(resources.NullToEmpty(), (k,id) => k.PairWithValue(id)).ToArray(),
                    confirmedNextId, confirmedWhen, confirmedById,
                    security,
                () => onCreated(),
                () => onAlreadyExists(),
                () => onStageNotFound(),
                (why) => onFailure(why));
        }
        
        [EastFive.Api.HttpPatch(Type = typeof(Resources.ProcessStep), MatchAllBodyParameters = false)]
        public static Task<HttpResponseMessage> UpdateConnectorAsync(
                [Property(Name = Resources.ProcessStep.IdPropertyName)]Guid id,
                [PropertyOptional(Name = Resources.ProcessStep.ConfirmedNextPropertyName)]Guid? confirmedNextId,
                [PropertyOptional(Name = Resources.ProcessStep.ConfirmedByPropertyName)]Guid? confirmedById,
                [PropertyOptional(Name = Resources.ProcessStep.ConfirmedWhenPropertyName)]DateTime? confirmedWhen,
                [PropertyOptional(Name = Resources.ProcessStep.ResourceKeysPropertyName)]string[] resourceKeys,
                [PropertyOptional(Name = Resources.ProcessStep.ResourcesPropertyName)]Guid[] resources,
                EastFive.Api.Controllers.Security security, UrlHelper url,
            NoContentResponse onUpdated,
            NotFoundResponse onNotFound,
            UnauthorizedResponse onUnauthorized,
            GeneralConflictResponse onFailure)
        {
            throw new NotImplementedException();
            //return Connectors.UpdateConnectorAsync(id,
            //        Flow, security.performingAsActorId, security.claims,
            //    () => onUpdated(),
            //    () => onNotFound(),
            //    (why) => onFailure(why));
        }

        [EastFive.Api.HttpDelete]
        public static Task<HttpResponseMessage> DeleteByIdAsync(
                [QueryDefaultParameter][Required(Name = Resources.ProcessStep.IdPropertyName)]Guid processStepId,
                EastFive.Api.Controllers.Security security,
            NoContentResponse onDeleted,
            NotFoundResponse onNotFound,
            UnauthorizedResponse onUnauthorized)
        {
            return Processes.DeleteByIdAsync(processStepId, security,
                () => onDeleted(),
                () => onNotFound(),
                () => onUnauthorized());
        }

        [EastFive.Api.HttpOptions(MatchAllBodyParameters = false)]
        public static HttpResponseMessage Options(HttpRequestMessage request, UrlHelper url, Application application,
            ContentResponse onOption)
        {
            return onOption(
                GetResource(
                    new Process()
                    {
                        processStageId = Guid.NewGuid(),
                        createdOn = DateTime.UtcNow,
                        processId = Guid.NewGuid(),
                        resourceId = Guid.NewGuid(),
                        resourceType = typeof(EastFive.Azure.ProcessStage),
                        confirmedBy = Guid.NewGuid(),
                        confirmedWhen = DateTime.UtcNow,
                        previousStep = Guid.NewGuid(),
                        resources = Enumerable
                            .Range(0, 3)
                            .Select(
                                i => new Process.ProcessStageResource()
                                {
                                    key = $"key{i}",
                                    resourceId = Guid.NewGuid(),
                                    type = typeof(Process),
                                })
                            .ToArray(),
                    },
                    application, url));
        }

    }
}

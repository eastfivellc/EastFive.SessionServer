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
    [FunctionViewController(
        Route = "ProcessResourceView",
        Resource = typeof(Resources.ProcessResourceView),
        ContentType = "x-application/process-resource-view",
        ContentTypeVersion = "0.1")]
    public class ProcessResourceViewController
    {
        #region GET
        
        [EastFive.Api.HttpGet]
        public static async Task<HttpResponseMessage> FindByResourceAsync(
                [Required(Name = Resources.ProcessResourceView.ActorPropertyName)]Guid actorId,
                [Required(Name = Resources.ProcessStageType.ResourceTypePropertyName)]Type resourceType,
                EastFive.Api.Controllers.Security security, Application application, UrlHelper url,
            MultipartAcceptArrayResponseAsync onMultipart,
            ReferencedDocumentNotFoundResponse onResourceNotFound,
            UnauthorizedResponse onUnauthorized)
        {
            return await await ProcessResourceViews.FindByResourceAsync(actorId, resourceType,
                    security,
                (views) => onMultipart(views.Select(ps => GetResource(ps, application, url))),
                () => onResourceNotFound().ToTask(),
                () => onUnauthorized().ToTask());
        }

        internal static Resources.ProcessResourceView GetResource(ProcessResourceView view, Application application, UrlHelper url)
        {
            return new Resources.ProcessResourceView
            {
                Id = url.GetWebId<ProcessResourceViewController>(view.processViewId),
                Actor = application.GetActorLink(view.actorId, url),
                Resource = application.GetResourceLink(view.resourceType, view.resourceId, url),
                ResourceType = application.GetResourceMime(view.resourceType),

                CurrentProcessStep = url.GetWebId<ProcessStepController>(view.currentProcessStepId),
                Titles = view.titles,
                Completions = view.completions,
                Invalidations = view.invalidations,

                ResourcesDisplayed = view.displayResources,
                ResourcesProvided = view.resourcesProvided
                    .Select(
                        resourceProvided => new Resources.ProcessResourceView.ConfirmableResource
                        {
                            Key = resourceProvided.key,
                            Resource = application.GetResourceLink(resourceProvided.type, resourceProvided.resourceId, url),
                            Type = application.GetResourceMime(resourceProvided.type),
                        })
                    .ToArray(),
                
                NextStages = view.nextStages
                    .Select(nextStageId => url.GetWebId<ProcessStage>(nextStageId.processStageId))
                    .ToArray(),
                Editable = view.editable,
                Completable = view.completable,
            };
        }

        #endregion
        

        [EastFive.Api.HttpOptions(MatchAllBodyParameters = false)]
        public static HttpResponseMessage Options(HttpRequestMessage request, Application application, UrlHelper url,
            ContentResponse onOption)
        {
            return onOption(
                GetResource(
                    new ProcessResourceView()
                    {
                        processViewId = Guid.NewGuid(),
                        actorId = Guid.NewGuid(),
                        resourceId = Guid.NewGuid(),
                        resourceType = typeof(Process),

                        currentProcessStepId = Guid.NewGuid(),
                        titles = new string[] { "Step 1", "Step 2", "Step 1", "Step 3" },
                        completions = new DateTime?[]
                            {
                                DateTime.UtcNow - TimeSpan.FromDays(4.0),
                                default(DateTime?),
                                DateTime.UtcNow - TimeSpan.FromDays(2.0),
                                DateTime.UtcNow - TimeSpan.FromDays(1.0),
                            },
                        invalidations = new DateTime?[]
                            {
                                default(DateTime?),
                                DateTime.UtcNow - TimeSpan.FromDays(3.0),
                                default(DateTime?),
                                default(DateTime?),
                            },

                        displayResources = new string[] { "process", "process" },
                        resourcesProvided = new Process.ProcessStageResource[]
                        {
                            new Process.ProcessStageResource
                            {

                            },
                            new Process.ProcessStageResource
                            {

                            },
                        },

                        nextStages = new ProcessStage[]
                        {
                            new ProcessStage
                            {
                                processStageId = Guid.NewGuid(),
                            }
                        },
                        editable = true,
                        completable = true,
                    },
                    application,
                    url));
        }

    }
}

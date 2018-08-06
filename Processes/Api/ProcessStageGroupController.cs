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

using BlackBarLabs.Extensions;
using EastFive.Azure.Api.Resources;

namespace EastFive.Azure.Api.Controllers
{
    [FunctionViewController(Route = "ProcessStageGroup")]
    public class ProcessStageGroupController
    {
        internal static Resources.ProcessStageGroup[] stages = new[]
        {
            new Resources.ProcessStageGroup
            {
                Id = ProcessStagesGroups.group1Id,
                Rank = 1.0,
                Title = "Ordered",
            },
            new Resources.ProcessStageGroup
            {
                Id = ProcessStagesGroups.group2Id,
                Rank = 2.0,
                Title = "Confirmed",
            },
            //new ProcessStageGroup
            //{
            //    Id = Guid.Parse("4b879bad-6543-4944-9a97-642661090176"),
            //    Rank = 3.0,
            //    Title = "Complete",
            //},
        };

        #region GET

        [EastFive.Api.HttpGet]
        public static Task<HttpResponseMessage> FindByIdAsync(
                [QueryDefaultParameter][Required]Guid id,
                EastFive.Api.Controllers.Security security, HttpRequestMessage request, UrlHelper url,
            ContentResponse onFound,
            NotFoundResponse onNotFound,
            UnauthorizedResponse onUnauthorized)
        {
            return stages.First(
                async (stage, next) =>
                {
                    if (stage.Id.UUID == id)
                        return onFound(stage);
                    return await next();
                },
                () => onNotFound().ToTask());

            //return Connectors.FindByIdAsync(id,
            //        security.performingAsActorId, security.claims,
            //    (synchronization, destinationIntegrationId) => onFound(GetResource(synchronization, destinationIntegrationId, url)),
            //    () => onNotFound(),
            //    () => onUnauthorized());
        }
        
        [EastFive.Api.HttpGet]
        public static Task<HttpResponseMessage> FindAllAsync(
                EastFive.Api.Controllers.Security security, HttpRequestMessage request, UrlHelper url,
            MultipartAcceptArrayResponseAsync onMultipart)
        {
            return onMultipart(stages);
        }
        

        #endregion
        

        [EastFive.Api.HttpPut(Type = typeof(EastFive.Api.Resources.Connector), MatchAllBodyParameters = false)]
        public static Task<HttpResponseMessage> UpdateConnectorAsync([PropertyGuid]Guid id,
                [PropertyGuid(Name = EastFive.Api.Resources.Connector.SourcePropertyName)]Guid source,
                [PropertyGuid(Name = EastFive.Api.Resources.Connector.DestinationPropertyName)]Guid? destination,
                [PropertyEnum(Name = EastFive.Api.Resources.Connector.FlowPropertyName)]Connector.SynchronizationMethod Flow,
                [PropertyGuid(Name = EastFive.Api.Resources.Connector.DestinationIntegrationPropertyName)]Guid? destinationIntegration,
                EastFive.Api.Controllers.Security security, HttpRequestMessage request, UrlHelper url,
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

        [EastFive.Api.HttpOptions(MatchAllBodyParameters = false)]
        public static HttpResponseMessage Options(HttpRequestMessage request, UrlHelper url,
            ContentResponse onOption)
        {
            return onOption(stages[1]);
        }

    }
}

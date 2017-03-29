using System;
using System.Threading.Tasks;
using System.Web.Http;
using BlackBarLabs.Api;
using System.Web.Mvc;
using System.Net.Http;
using BlackBarLabs.Extensions;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class ActAsUserController : BaseController
    {
        public IHttpActionResult Get([FromUri]Resources.Queries.ActAsUserQuery query)
        {
            return this.ActionResult(() => query.GetAsync(this.Request, this.Url, this.Redirect));
        }

        public IHttpActionResult Options(Nullable<Guid> Id = default(Nullable<Guid>))
        {
            return this.ActionResult(
                () =>
                {
                    var request = this.Request;
                    return request.GetActorIdClaimsAsync(
                        (actorId, claims) =>
                        {
                            var responseAllowed = request.CreateResponse(System.Net.HttpStatusCode.OK);
                            responseAllowed.Content = new StringContent(String.Empty);
                            responseAllowed.Content.Headers.Add("Access-Control-Expose-Headers", "Allow");

                            var superAdminId = default(Guid);
                            var superAdminIdStr = EastFive.Web.Configuration.Settings.Get(
                                EastFive.Api.Configuration.SecurityDefinitions.ActorIdSuperAdmin);
                            if (!Guid.TryParse(superAdminIdStr, out superAdminId))
                            {
                                responseAllowed.AddReason($"Configuration parameter [{EastFive.Api.Configuration.SecurityDefinitions.ActorIdSuperAdmin}] is not set");
                                return responseAllowed.ToTask();
                            }
                            if (actorId != superAdminId)
                            {
                                responseAllowed.AddReason($"Actor [{actorId}] is not site admin");
                                return responseAllowed.ToTask();
                            }

                            responseAllowed.Content.Headers.Allow.Add(HttpMethod.Get.Method);
                            return responseAllowed.ToTask();
                        });
                });
        }
    }
}

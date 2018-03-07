using BlackBarLabs.Api;
using Newtonsoft.Json;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using BlackBarLabs.Extensions;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class AppSetting
    {
        [JsonProperty(PropertyName = "Name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "Value")]
        public string Value { get; set; }
    }

    [RoutePrefix("aadb2c")]
    public class DiagnosticsController : BaseController
    {
        [HttpGet]
        public async Task<HttpResponseMessage> Get([FromUri]AccountLinksQuery q)
        {
            return await this.Request.GetActorIdClaimsAsync((actorId, claims) =>
            {
                return EastFive.Web.Configuration.Settings.GetGuid(
                    EastFive.Api.AppSettings.ActorIdSuperAdmin,
                    (actorIdSuperAdmin) =>
                    {
                        if (actorIdSuperAdmin == actorId)
                        {
                            var settings = ConfigurationManager.AppSettings.AllKeys
                                .Select(x => new AppSetting { Name = x, Value = ConfigurationManager.AppSettings[x] }).OrderBy(x => x.Name).ToArray();
                            return this.Request.CreateResponse(System.Net.HttpStatusCode.OK, settings, "application/json").ToTask();
                        }
                        return this.Request.CreateResponse(System.Net.HttpStatusCode.NotFound).ToTask();
                    },
                    (why) => this.Request.CreateResponse(System.Net.HttpStatusCode.InternalServerError, why).ToTask());
            });
        }
    }
}
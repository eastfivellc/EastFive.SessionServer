using BlackBarLabs.Api;
using EastFive.Security.SessionServer.Api.Resources;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Routing;
using BlackBarLabs;
using System;
using EastFive.Api.Services;
using BlackBarLabs.Extensions;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    [RoutePrefix("api")]
    public class InviteController : BaseController
    {
        #region Get

        public IHttpActionResult Get([FromUri]Resources.Queries.InviteQuery model)
        {
            return new HttpActionResult(() => model.QueryAsync(this.Request, this.Url));
        }

        #endregion

        public IHttpActionResult Post([FromBody]Resources.Invite model)
        {
            return new HttpActionResult(() => model.CreateAsync(this.Request, this.Url));
        }
    }
}


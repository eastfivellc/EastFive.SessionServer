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
using EastFive.Api.Azure.Controllers;
using EastFive.Security.SessionServer.Api.Resources.Queries;

namespace EastFive.Api.Azure.Credentials.Controllers
{
    [RoutePrefix("api")]
    public class TokenCredentialController : BaseController
    {
        #region Get

        public IHttpActionResult Get([FromUri]TokenCredentialQuery model)
        {
            return new HttpActionResult(() => model.QueryAsync(this.Request, this.Url));
        }

        #endregion

        public IHttpActionResult Post([FromBody]TokenCredential model)
        {
            return new HttpActionResult(() => model.CreateAsync(this.Request, this.Url));
        }
        public IHttpActionResult Put([FromBody]TokenCredential model)
        {
            return new HttpActionResult(() => model.UpdateAsync(this.Request, this.Url));
        }

        public IHttpActionResult Delete([FromBody]TokenCredentialQuery model)
        {
            return new HttpActionResult(() => model.DeleteAsync(this.Request, this.Url));
        }
    }
}


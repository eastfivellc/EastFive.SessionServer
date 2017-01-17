using BlackBarLabs.Api;
using System;
using System.Web.Http;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class ClaimController : BaseController
    {
        public IHttpActionResult Get([FromUri]Resources.Claim model)
        {
            return new HttpActionResult(() => model.GetAsync(this.Request));
        }

        public IHttpActionResult Post([FromBody]Resources.Claim model)
        {
            return new HttpActionResult(() => model.CreateAsync(this.Request));
        }

        public IHttpActionResult Put([FromBody]Resources.Claim model)
        {
            return new HttpActionResult(() => model.UpdateAsync(this.Request));
        }
    }
}


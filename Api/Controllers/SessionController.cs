using System;
using System.Web.Http;
using System.Threading.Tasks;

using BlackBarLabs.Api;
using System.Net.Http;
using System.Net;
using BlackBarLabs.Extensions;
using EastFive.Api.Azure.Controllers;

namespace EastFive.Api.Azure.Credentials.Controllers
{
    public class SessionController : BaseController
    {
        //public IHttpActionResult Post([FromBody]Resources.Session model)
        //{
        //    return new HttpActionResult(() => model.CreateAsync(this.Request, this.Url));
        //}

        //public IHttpActionResult Get([FromUri]Resources.Queries.SessionQuery model)
        //{
        //    return new HttpActionResult(() => model.QueryAsync(this.Request, this.Url));
        //}

        //public IHttpActionResult Put([FromBody]Resources.Session model)
        //{
        //    return this.ActionResult(() => model.UpdateAsync(this.Request));
        //}

        public IHttpActionResult Delete([FromBody]Resources.Queries.SessionQuery query)
        {
            return this.ActionResult(() => query.DeleteAsync(this.Request, this.Url));
        }
    }
}


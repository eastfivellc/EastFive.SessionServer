using System;
using System.Threading.Tasks;
using System.Web.Http;
using BlackBarLabs.Api;
using EastFive.Api.Azure.Controllers;
using EastFive.Security.SessionServer.Api.Resources;

namespace EastFive.Api.Azure.Controllers
{
    public class RoleController : BaseController
    {
        public IHttpActionResult Get([FromUri]Resources.Queries.RoleQuery query)
        {
            return this.ActionResult(() => query.GetAsync(this.Request, this.Url));
        }

        public IHttpActionResult Post([FromBody]Resources.Role role)
        {
            return this.ActionResult(() => role.CreateAsync(this.Request, this.Url));
        }
        
        public IHttpActionResult Put([FromBody]Resources.Role role)
        {
            HttpActionDelegate callback = () => role.UpdateAsync(this.Request, this.Url);
            return callback.ToActionResult();
        }
        
        public IHttpActionResult Delete([FromBody]Resources.Queries.RoleQuery role)
        {
            HttpActionDelegate callback = () => role.DeleteAsync(this.Request, this.Url);
            return callback.ToActionResult();
        }
    }
}

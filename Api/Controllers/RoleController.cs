using System.Threading.Tasks;
using System.Web.Http;
using OrderOwl.Api.Models;
using BlackBarLabs.Api;
using System;

namespace OrderOwl.Api.Controllers
{
    public class RoleController : BaseController
    {
        public IHttpActionResult Get([FromUri]Resources.RoleQuery query)
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
        
        public IHttpActionResult Delete([FromBody]Resources.RoleQuery role)
        {
            HttpActionDelegate callback = () => role.DeleteAsync(this.Request, this.Url);
            return callback.ToActionResult();
        }
    }
}

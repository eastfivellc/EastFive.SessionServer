using System;
using System.Web.Http;
using System.Threading.Tasks;

using BlackBarLabs.Api;
using System.Net.Http;
using System.Net;
using BlackBarLabs.Extensions;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class SessionController : BaseController
    {   
        public IHttpActionResult Post([FromBody]Resources.Session model)
        {
            return new HttpActionResult(() => model.CreateAsync(this.Request, this.Url));
        }

        public IHttpActionResult Get([FromUri]Resources.Queries.SessionQuery model)
        {
            return new HttpActionResult(() => model.QueryAsync(this.Request, this.Url));
        }
        
        public IHttpActionResult Put([FromBody]Resources.Session model)
        {
            return this.ActionResult(() => model.UpdateAsync(this.Request));
        }

        public IHttpActionResult Delete([FromBody]Resources.Queries.SessionQuery query)
        {
            return this.ActionResult(() => query.DeleteAsync(this.Request, this.Url));
        }

        public IHttpActionResult Options(Nullable<Guid> Id = default(Nullable<Guid>))
        {
            var post1 = new Resources.Session
            {
                Id = Guid.NewGuid(),
            };
            var response = new BlackBarLabs.Api.Resources.Options
            {
                Post = new[] { post1 },
            };
            if(default(Nullable<Guid>) != Id)
            {
                var authId = Guid.NewGuid();
                var jwtToken = BlackBarLabs.Security.Tokens.JwtTools.CreateToken(
                    Id.Value, authId, new Uri("http://example.com"), TimeSpan.FromDays(200000),
                    (token) => token,
                    (config) => string.Empty,
                    (config, why) => string.Empty);

                var put1 = new Resources.Session
                {
                    Id = Id.Value,
                    AuthorizationId = authId,
                };
                response.Put = new[] { put1 };
            }
            return Json(response);
        }
    }
}


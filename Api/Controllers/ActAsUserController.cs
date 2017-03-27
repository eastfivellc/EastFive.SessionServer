using System;
using System.Threading.Tasks;
using System.Web.Http;
using BlackBarLabs.Api;
using System.Web.Mvc;

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
            var post1 = new Resources.Session
            {
                Id = Guid.NewGuid(),
            };
            var response = new BlackBarLabs.Api.Resources.Options
            {
                Post = new[] { post1 },
            };
            if (default(Nullable<Guid>) != Id)
            {
                var authId = Guid.NewGuid();
                var jwtToken = BlackBarLabs.Security.Tokens.JwtTools.CreateToken(
                    Id.Value, authId, new Uri("http://example.com"), TimeSpan.FromDays(200000),
                    (token) => token,
                    (config) => string.Empty,
                    (config, why) => string.Empty,
                    "AuthServer.issuer", "AuthServer.key");

                var put1 = new Resources.Session
                {
                    Id = Id.Value,
                    AuthorizationId = authId,
                    SessionHeader = new Resources.AuthHeaderProps
                    {
                        Name = "Authorization",
                        Value = jwtToken,
                    }
                };
                response.Put = new[] { put1 };
            }
            return Json(response);
        }
    }
}

using System;
using System.Threading.Tasks;
using System.Web.Http;
using BlackBarLabs.Api;

namespace EastFive.Api.Azure.Credentials.Controllers
{
    public class TokenController : ResponseController
    {
        public override Task<IHttpActionResult> Get([FromUri]ResponseResult query)
        {
            query.method = CredentialValidationMethodTypes.Token;
            return base.Get(query);
        }
    }
}

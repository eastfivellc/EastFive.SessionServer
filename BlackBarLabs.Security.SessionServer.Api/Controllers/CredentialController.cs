using BlackBarLabs.Security.AuthorizationServer.API.Resources;
using BlackBarLabs.Security.SessionServer.Api.Resources;
using System.Web.Http;

namespace BlackBarLabs.Security.SessionServer.Api.Controllers
{
    public class CredentialController : BaseController
    {
        public IHttpActionResult Post([FromBody]CredentialPost model)
        {
            model.Request = Request;
            return model;
        }

        public IHttpActionResult Put([FromBody]CredentialPut model)
        {
            model.Request = Request;
            return model;
        }

        public IHttpActionResult Delete([FromBody]CredentialDelete model)
        {
            model.Request = Request;
            return model;
        }
        public IHttpActionResult Get([FromUri]CredentialGet model)
        {
            model.Request = Request;
            return model;
        }

        //public IHttpActionResult Options()
        //{
        //    var model = new Resources.CredentialOptions();
        //    return model;
        //}
    }
}


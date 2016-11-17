using System.Web.Http;

namespace BlackBarLabs.Security.AuthorizationServer.API.Controllers
{
    public class CredentialController : BaseController
    {
        public IHttpActionResult Post([FromBody]Resources.CredentialPost model)
        {
            model.Request = Request;
            return model;
        }

        public IHttpActionResult Put([FromBody]Resources.CredentialPut model)
        {
            model.Request = Request;
            return model;
        }

        public IHttpActionResult Delete([FromBody]Resources.CredentialDelete model)
        {
            model.Request = Request;
            return model;
        }
        public IHttpActionResult Get([FromUri]Resources.CredentialGet model)
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


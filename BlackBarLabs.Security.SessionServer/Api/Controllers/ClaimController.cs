using System;
//using BlackBarLabs.Security.Tokens;
using BlackBarLabs.Security.Session;
using System.Web.Http;
using BlackBarLabs.Security.SessionServer.Api.Resources;

namespace BlackBarLabs.Security.SessionServer.Api.Controllers
{
    public class ClaimController : BaseController
    {
        public IHttpActionResult Get([FromUri]ClaimGet model)
        {
            model.Request = Request;
            return model;
        }

        public IHttpActionResult Post([FromBody]Resources.ClaimPost model)
        {
            model.Request = Request;
            return model;
        }

        public IHttpActionResult Put([FromBody]Resources.ClaimPut model)
        {
            model.Request = Request;
            return model;
        }
    }
}


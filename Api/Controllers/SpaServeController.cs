using System;
using System.Threading.Tasks;
using System.Web.Http;
using BlackBarLabs.Api;
using System.Web.Mvc;
using System.Net.Http;
using BlackBarLabs.Extensions;
using System.Net;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class SpaServeController : BaseController
    { 
        public IHttpActionResult Get([FromUri]string id)
        {
            var response = Request.CreateResponse(HttpStatusCode.OK);

            var content = Properties.Resources.spahead + "|" + Properties.Resources.spabody;

            response.Content = new StringContent(content);


            return response.ToActionResult();
        }
    }
}

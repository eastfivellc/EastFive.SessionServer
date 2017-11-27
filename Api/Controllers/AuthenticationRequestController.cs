using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

using BlackBarLabs;
using BlackBarLabs.Api;
using EastFive.Api.Services;
using BlackBarLabs.Extensions;
using EastFive.Security.CredentialProvider.AzureADB2C;

namespace EastFive.Security.SessionServer.Api.Controllers
{   
    public class AuthenticationRequestController : BaseController
    {
        public IHttpActionResult Post([FromBody]Resources.AuthenticationRequest model)
        {
            return new HttpActionResult(() => model.CreateAsync(this.Request, this.Url));
        }

        public IHttpActionResult Get([FromUri]Resources.Queries.AuthenticationRequestQuery model)
        {
            return new HttpActionResult(() => model.QueryAsync(this.Request, this.Url));
        }
    }
}
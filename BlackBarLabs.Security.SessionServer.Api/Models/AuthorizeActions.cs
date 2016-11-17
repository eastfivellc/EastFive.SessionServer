using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace BlackBarLabs.Security.AuthorizationServer.API.Models
{
    public static class AuthorizeActions
    {
        public static Task<HttpResponseMessage> PostAsync(this Resources.Authorize resource, HttpRequestMessage request)
        {
            throw new NotImplementedException();
        }
    }
}
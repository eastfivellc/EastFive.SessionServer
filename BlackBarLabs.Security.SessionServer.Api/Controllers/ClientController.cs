using System.Security.Cryptography;
using System.Web.Http;
using Microsoft.Owin.Security.DataHandler.Encoder;
using NC2.Security.AuthorizationServer.API.Models;

namespace NC2.Security.AuthorizationServer.API.Controllers
{
    public class ClientController : BaseController
    {
        public IHttpActionResult Post(ClientModel clientModel)
        {
            if (!ModelState.IsValid) {
                return BadRequest(ModelState);
            }

            var key = new byte[32];
            RandomNumberGenerator.Create().GetBytes(key);
            var base64Secret = TextEncodings.Base64Url.Encode(key);

            var client = DataContext.ClientCollection.CreateAsync(base64Secret, clientModel.Name);

            return Ok();
            //return Ok<ClientModel>(client);

        }
    }
}

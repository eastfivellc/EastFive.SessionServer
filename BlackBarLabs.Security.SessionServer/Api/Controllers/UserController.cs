using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;
using NC2.CPM.Prize.API.Models;
using NC2.CPM.Security.Crypto;
using NC2.Security.AuthorizationServer.API.Models;
using NC2.Security.AuthorizationServer.Business.Users;
using Newtonsoft.Json.Linq;

namespace NC2.Security.AuthorizationServer.API.Controllers
{
    public class UserController : BaseController
    {

        //[System.Web.Mvc.HttpGet]
        //public async Task<ActionResult> Index(SessionFilter filter)
        //{
        //    var viewModel = await (new SessionModel { Id = filter.Id }).ResolveAsync(DataContext, Request.GetHttpMethod(), Url, ControllerContext.RequestContext).ConfigureAwait(false);
        //    return View(viewModel);
        //}

        //[System.Web.Mvc.AcceptVerbs(HttpVerbs.Post | HttpVerbs.Put)]
        //public async Task<ActionResult> Index(SessionModel model)
        //{
        //    var viewModel = await model.ResolveAsync(DataContext, Request.GetHttpMethod(), Url, ControllerContext.RequestContext).ConfigureAwait(false);
        //    return View(viewModel);
        //}

        [System.Web.Mvc.HttpGet]
        public async Task<ActionResult> Index(string emailAddress)
        {
            //TODO
            var user = await DataContext.UserCollection.FindByUserIdAsync(emailAddress).ConfigureAwait(false);
            if (user == null) throw new InvalidOperationException("User not found with emailAddress: " + emailAddress);
            bool isAuthenticated = false;
            return View(isAuthenticated);
        }

        [System.Web.Mvc.HttpPost]
        public async Task<ActionResult> Create(string deviceSessionId, string userName, string password)
        {
            //Create the Auth Server User
            var hash = CryptoTools.GenerateHash(password);
            var user = await DataContext.UserCollection.CreateAsync(userName, hash.Hash, hash.Salt).ConfigureAwait(false);

            //Generate Token for this User Authenticated
            var scheme = Request.Url.Scheme;
            var server = Request.Url.Host;
            var port = Request.Url.Port;

            var content = new StringContent(string.Format("grant_type=password&username={0}&password={1}&session_id={2}",userName, password, deviceSessionId), Encoding.UTF8, "application/x-www-form-urlencoded");
            var authRequest = new HttpClient();


            authRequest.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(Request.Headers["Authorization"]);
            var response = await authRequest.PostAsync(scheme + "://" + server + ":" + port + "/" + "/oauth2/token", content);

            if (response.IsSuccessStatusCode)
            {
                var resp = await response.Content.ReadAsStringAsync();
                JObject responseItems = JObject.Parse(resp);
                var accessTokenModel = new AccessToken()
                {
                    access_token = (string) responseItems["access_token"],
                    token_type = (string) responseItems["token_type"],
                    expires_in = (string) responseItems["expires_in"]
                };
                return View(accessTokenModel);
            }

            throw new InvalidOperationException("Error creating user");
        }

        [System.Web.Mvc.HttpGet]
        public async Task<ActionResult> UpdatePassword(string emailAddress)
        {
            var user = await DataContext.UserCollection.FindByUserIdAsync(emailAddress).ConfigureAwait(false);
            if (user == null) throw new InvalidOperationException("User not found with emailAddress: " + emailAddress);
            var model = new UpdatePassword {Email = emailAddress};
            return View(model);
        }

        [System.Web.Mvc.HttpPost]
        public async Task<ActionResult> UpdatePassword([FromBody]UpdatePassword update)
        {
            if (update.NewPassword != update.ConfirmNewPassword) throw new InvalidOperationException("New password values do not match");

            var user = await DataContext.UserCollection.FindByUserIdAsync(update.Email).ConfigureAwait(false);
            if (user == null) throw new InvalidOperationException("User not found with email: " + update.Email);

            // TODO:send pass changed email here?
            bool isAuthenticated = false;
            //var model = new User(user.Id, update.Email, isAuthenticated, Url, ControllerContext.RequestContext);
            return View("Index", "");
        }

    }
}



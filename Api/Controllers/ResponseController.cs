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
using BlackBarLabs.Collections.Generic;
using EastFive.Collections.Generic;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    public class ResponseResult
    {
        public CredentialValidationMethodTypes method { get; set; }
    }

    [RoutePrefix("aadb2c")]
    public class ResponseController : BaseController
    {
        public virtual async Task<IHttpActionResult> Get([FromUri]ResponseResult result)
        {
            if (result.IsDefault())
                return this.Request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason("Method not provided in response")
                    .ToActionResult();

            var kvps = Request.GetQueryNameValuePairs();
            return await ProcessRequestAsync(result.method, kvps.ToDictionary());
        }

        public virtual async Task<IHttpActionResult> Post([FromUri]ResponseResult result)
        {
            if (result.IsDefault())
                return this.Request.CreateResponse(HttpStatusCode.Conflict)
                    .AddReason("Method not provided in response")
                    .ToActionResult();

            var kvps = Request.GetQueryNameValuePairs();
            var bodyValues = await await Request.Content.ReadFormDataAsync(
                (values) => values.AllKeys
                    .Select(v => v.PairWithValue(values[v]))
                    .ToArray()
                    .ToTask(),
                async () => await await Request.Content.ReadMultipartContentAsync(
                    values => values
                        .Select(async v => v.Key.PairWithValue(await v.Value.ReadAsStringAsync()))
                        .WhenAllAsync(),
                    () => (new KeyValuePair<string, string>()).AsArray().ToTask()));
            var allrequestParams = kvps.Concat(bodyValues).ToDictionary();
            return await ProcessRequestAsync(result.method, allrequestParams);
        }

        protected async Task<IHttpActionResult> ProcessRequestAsync(CredentialValidationMethodTypes method,
            IDictionary<string, string> values)
        {
            var context = this.Request.GetSessionServerContext();
            var response = await await context.AuthenticationRequests.UpdateAsync(Guid.NewGuid(),
                    method, values,
                (sessionId, authorizationId, jwtToken, refreshToken, action, extraParams, redirectUrl) =>
                    CreateResponse(context, method, action, sessionId, authorizationId, jwtToken, refreshToken, extraParams, redirectUrl),
                (existingId) => this.Request.CreateResponse(HttpStatusCode.InternalServerError)
                        .AddReason("GUID NOT UNIQUE")
                        .ToActionResult()
                        .ToTask(),
                (why) => this.Request.CreateResponse(HttpStatusCode.Conflict)
                            .AddReason($"Invalid token:{why}")
                            .ToActionResult()
                            .ToTask(),
                () => this.Request.CreateResponse(HttpStatusCode.Conflict)
                            .AddReason($"Token is not connected to a user in this system")
                            .ToActionResult()
                            .ToTask(),
                (why) => this.Request.CreateResponse(HttpStatusCode.ServiceUnavailable)
                            .AddReason(why)
                            .ToActionResult()
                            .ToTask(),
                (why) => this.Request.CreateResponse(HttpStatusCode.InternalServerError)
                            .AddReason(why)
                            .ToActionResult()
                            .ToTask(),
                (why) => this.Request.CreateResponse(HttpStatusCode.Conflict)
                            .AddReason(why)
                            .ToActionResult()
                            .ToTask());
            return response;
        }

        private async Task<IHttpActionResult> CreateResponse(Context context,
            CredentialValidationMethodTypes method, AuthenticationActions action,
            Guid sessionId, Guid? authorizationId, string jwtToken, string refreshToken,
            IDictionary<string, string> extraParams, Uri redirectUrl)
        {
            var config = Library.configurationManager;
            var redirectResponse = await config.GetRedirectUriAsync(context, method, action,
                    authorizationId, jwtToken, refreshToken, extraParams,
                    redirectUrl,
                (redirectUrlSelected) => Redirect(redirectUrlSelected),
                (paramName, why) => Request.CreateResponse(HttpStatusCode.BadRequest)
                    .AddReason(why)
                    .ToActionResult(),
                (why) => Request.CreateResponse(HttpStatusCode.BadRequest)
                    .AddReason(why)
                    .ToActionResult());
            return redirectResponse;
        }
    }
}

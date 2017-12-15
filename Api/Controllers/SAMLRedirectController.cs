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
using System.Xml;
using BlackBarLabs.Extensions;
using System.Xml.Linq;
using Newtonsoft.Json;
using System.Dynamic;
using EastFive.Security.CredentialProvider.SAML;
using BlackBarLabs.Linq;
using BlackBarLabs.Collections.Generic;
using EastFive.Collections.Generic;

namespace EastFive.Security.SessionServer.Api.Controllers
{
    // aadb2c/SAMLRedirect?tokenid=ID7ee36a406286079a7237b23dd7647d95b8d42ddbcde4fbe8030000015d5790a407&agentid=e924bba8
    [RoutePrefix("aadb2c")]
    public class SAMLRedirectController : BaseController
    {
        public async Task<IHttpActionResult> Post()
        {
            //return await await this.Request.Content.ParseMultipartAsync(
            //    (string SAMLResponse) =>
            //    {
            //        return ParseSAMLResponseAsync(SAMLResponse);
            //    });
            return await await this.Request.Content.ParseMultipartAsync(
                (byte[] SAMLResponse) => ParseSAMLResponseAsync(SAMLResponse));
        }

        private TResult ParseToDictionary<TResult>(string samlResponse,
            Func<IDictionary<string, string>, TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            try
            {
                var doc = XDocument.Parse(samlResponse); //or XDocument.Load(path)
                string jsonText = JsonConvert.SerializeXNode(doc);
                var dyn = JsonConvert.DeserializeObject<ExpandoObject>(jsonText);

                var response = ((IDictionary<string, object>)dyn)[SAMLProvider.SamlpResponseKey];
                var assertion = (IDictionary<string, object>)(((IDictionary<string, object>)response)[SAMLProvider.SamlAssertionKey]);
                var subject = assertion[SAMLProvider.SamlSubjectKey];
                var nameIdNode = ((IDictionary<string, object>)subject)[SAMLProvider.SamlNameIDKey];
                var nameId = (string)((IDictionary<string, object>)nameIdNode)["#text"];
                return onSuccess(
                    assertion
                        .Select(kvp => kvp.Key.PairWithValue(kvp.Value.ToString()))
                        .Append(SAMLProvider.SamlNameIDKey.PairWithValue(nameId))
                        .ToDictionary());
            }
            catch (Exception ex)
            {
                return onFailure(ex.Message);
            }
        }

        private async Task<IHttpActionResult> ParseSAMLResponseAsync(byte[] samlResponseBytes)
        {
            var samlResponse = System.Text.Encoding.UTF8.GetString(samlResponseBytes);
            if (String.IsNullOrWhiteSpace(samlResponse))
                return this.Request.CreateResponse(HttpStatusCode.Conflict)
                            .AddReason("SAML Response not provided in form POST")
                            .ToActionResult();

            return await ParseToDictionary(samlResponse,
                async (tokens) =>
                {
                    var context = Request.GetSessionServerContext();
                    return await await context.Sessions.AuthenticateAsync<Task<IHttpActionResult>>(Guid.NewGuid(),
                        CredentialValidationMethodTypes.SAML, tokens,
                        (sessionId, authorizationId, token, refreshToken, action, extraParams, redirectUri) =>
                        {
                            var config = Library.configurationManager;
                            var redirectResponse = config.GetRedirectUriAsync<IHttpActionResult>(context,
                                CredentialValidationMethodTypes.SAML, action,
                                sessionId, authorizationId, token, refreshToken, extraParams, redirectUri,
                                (redirectUrl) => Redirect(redirectUrl),
                                (paramName, why) => Request.CreateResponse(HttpStatusCode.BadRequest).AddReason(why).ToActionResult(),
                                (why) => Request.CreateResponse(HttpStatusCode.BadRequest).AddReason(why).ToActionResult());
                            return redirectResponse;
                        },
                        (location) => ((IHttpActionResult)Redirect(location)).ToTask(),
                        (why) => this.Request.CreateResponse(HttpStatusCode.BadRequest)
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
                        },
                        (why) => Request.CreateResponse(HttpStatusCode.BadRequest)
                            .AddReason(why)
                            .ToActionResult()
                            .ToTask());
            
        }
    }
}
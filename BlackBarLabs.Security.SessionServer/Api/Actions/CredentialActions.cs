using System;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Http;
using System.Net.Http;
using System.Threading;

using BlackBarLabs.Api;

namespace BlackBarLabs.Security.SessionServer.Api
{
    public static class CredentialActions
    {
        #region Actionables
        
        public static async Task<HttpResponseMessage> CreateAsync(this Resources.Credential credential,
            HttpRequestMessage request)
        {
            var context = request.GetSessionServerContext();
            var creationResults = await context.Authorizations.CreateCredentialsAsync(credential.AuthorizationId,
                credential.Method, credential.Provider, credential.UserId, credential.Token,
                () => request.CreateResponse(HttpStatusCode.Created, credential),
                (why) => request.CreateResponse(HttpStatusCode.Conflict).AddReason($"Authentication failed:{why}"),
                () => request.CreateResponse(HttpStatusCode.Conflict).AddReason("Authorization does not exist"),
                (alreadyAssociatedAuthId) =>
                {
                    var alreadyAssociatedAuthIdUrl = (string)"";
                    return request.CreateResponse(HttpStatusCode.Conflict, alreadyAssociatedAuthIdUrl);
                });
            return creationResults;
        }

        #endregion
    }
}

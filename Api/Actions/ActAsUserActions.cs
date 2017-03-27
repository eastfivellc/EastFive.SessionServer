using System;
using System.Net;
using System.Threading.Tasks;
using System.Net.Http;
using BlackBarLabs.Api;
using System.Web.Http.Routing;
using System.Configuration;
using EastFive.Api.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Http.Results;
using BlackBarLabs;

namespace EastFive.Security.SessionServer.Api
{
    public static class ActAsUserActions
    {

        public static async Task<HttpResponseMessage> GetAsync(this Resources.Queries.ActAsUserQuery query,
            HttpRequestMessage request, UrlHelper urlHelper, Func<string, RedirectResult> redirect)
        {
            return await query.ParseAsync(request,
                q => QueryByTokenAndActorIdAsync(q.RedirectUri.ParamValue(), q.Token.ParamValue(), q.ActorId.ParamSingle(), request, urlHelper, redirect),
                q => QueryAllActorsAsync(q.RedirectUri.ParamValue(), q.Token.ParamValue(), request, urlHelper));
        }

        public struct UserInfo
        {
            public string UserId;
            public string Link;
        }

        private static async Task<HttpResponseMessage> QueryAllActorsAsync(string redirectUri, string token,
         HttpRequestMessage request, UrlHelper url)
        {
            var urlQuery = request.RequestUri.Query;
            var baseUrl = url.GetLocationWithQuery(typeof(Controllers.ActAsUserController), urlQuery);
            
            var context = request.GetSessionServerContext();
            var userInfos = await context.PasswordCredentials.GetAllLoginInfoAsync(
                credentials =>
                {
                    var userInfo = credentials.Select(
                        info =>
                        {
                            return new UserInfo
                            {
                                UserId = info.UserId,
                                Link = baseUrl.AddParameter("query.actorId", info.LoginId.ToString()).ToString()
                            };
                        }).ToArray();
                    return userInfo;
                });

            var html = GenerateActAsUserHtml(userInfos);
            return request.CreateHtmlResponse(html);
        }

        private static string GenerateActAsUserHtml(UserInfo[] userInfos)
        {
            var tableContents = "";
            foreach (var userInfo in userInfos)
            {
                tableContents += $"<tr><td>{userInfo.UserId}</td><td><a href={userInfo.Link}>{userInfo.Link}</a></td></tr>";
            }
            var html = $"<html><body><table><tr><td>UserId</td><td>Link</td></tr>{tableContents}</table></body></html>";
            return html;
        }

        private static async Task<HttpResponseMessage> QueryByTokenAndActorIdAsync(string redirectBase, string token, Guid loginId, 
        HttpRequestMessage request, UrlHelper url, Func<string, RedirectResult> redirect)
        {
            var context = request.GetSessionServerContext();
            var result = await context.Sessions.LookupCredentialMappingAsync(loginId, Guid.NewGuid(), new Uri(redirectBase),
                (redirectUrlBase, authorizationId, tken, refreshToken) =>
                {
                    var redirectUrl = (new Uri(redirectBase))
                        .SetQueryParam("authoriationId", authorizationId.ToString("N"))
                        .SetQueryParam("token", tken)
                        .SetQueryParam("refreshToken", refreshToken);
                    var redirectResponse = redirect(redirectUrl.AbsoluteUri);
                    return request.CreateResponse(HttpStatusCode.OK);
                },
                () =>
                {
                    return request.CreateResponse(HttpStatusCode.Conflict);
                },
                () =>
                {
                    return request.CreateResponse(HttpStatusCode.Conflict);
                });
            return result;
        }
    }
}

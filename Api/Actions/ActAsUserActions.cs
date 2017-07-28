﻿using System;
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
            public Guid ActorId;
        }

        private static async Task<HttpResponseMessage> QueryAllActorsAsync(string redirectUri, string token,
            HttpRequestMessage request, UrlHelper url)
        {
            return await request.GetActorIdClaimsAsync(
                async (actorId, claims) =>
                {
                    var superAdminId = default(Guid);
                    var superAdminIdStr = EastFive.Web.Configuration.Settings.Get(
                        EastFive.Api.Configuration.SecurityDefinitions.ActorIdSuperAdmin);
                    if (!Guid.TryParse(superAdminIdStr, out superAdminId))
                    {
                        request.CreateResponse(HttpStatusCode.Unauthorized, $"Configuration parameter [{EastFive.Api.Configuration.SecurityDefinitions.ActorIdSuperAdmin}] is not set");
                    }
                    if (actorId != superAdminId)
                    {
                        request.CreateResponse(HttpStatusCode.Unauthorized, $"Actor [{actorId}] is not site admin");
                    }

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
                                        Link = baseUrl.AddParameter("ActorId", info.LoginId.ToString()).ToString(),
                                        ActorId = info.ActorId,
                                    };
                                }).ToArray();
                            return userInfo;
                        });

                    if (request.Headers.Accept.Where(accept => accept.MediaType == "application/json").Any())
                        return request.CreateResponse(HttpStatusCode.OK, userInfos);

                    var html = GenerateActAsUserHtml(userInfos);
                    return request.CreateHtmlResponse(html);
                });
        }
        
        private static string GenerateActAsUserHtml(UserInfo[] userInfos)
        {
            var tableContents = "";
            foreach (var userInfo in userInfos)
            {
                tableContents += $"<tr><td><a href=\"{userInfo.Link}\">Username: {userInfo.UserId}</a></td></tr>\n";
            }
            var html = $"<html><body><table><tr><th>UserId</th></tr>{tableContents}</table></body></html>";
            return html;
        }
        
        private static async Task<HttpResponseMessage> QueryByTokenAndActorIdAsync(string redirectBase, string token, Guid loginId, 
            HttpRequestMessage request, UrlHelper url, 
            Func<string, RedirectResult> redirect)
        {
            var context = request.GetSessionServerContext();
            var result = await context.Sessions.LookupCredentialMappingAsync(loginId, Guid.NewGuid(),
                new Dictionary<string, string>(),
                (authorizationId, tken, refreshToken, extraParams) =>
                {
                    return Library.configurationManager.GetRedirectUri(CredentialValidationMethodTypes.Password,
                        authorizationId, tken, refreshToken, extraParams,
                        (redirectUrl) =>
                        {
                            //var redirectResponse = redirect(redirectUrl.AbsoluteUri);
                            //return request.CreateResponse(HttpStatusCode.OK);
                            var response = request.CreateHtmlResponse($"<script>window.location=\"{redirectUrl}\"</script>");
                            var cookie = new System.Net.Http.Headers.CookieHeaderValue(Api.Constants.Cookies.FakingId, token);
                            cookie.Expires = DateTimeOffset.Now.AddDays(1);
                            cookie.Domain = request.RequestUri.Host;
                            cookie.Path = "/";
                            response.Headers.AddCookies(new System.Net.Http.Headers.CookieHeaderValue[] { cookie });
                            return response;
                        },
                        // TODO: Add reasons
                        (param, why) => request.CreateResponse(HttpStatusCode.Conflict),
                        (why) => request.CreateResponse(HttpStatusCode.Conflict));
                },
                () =>
                {
                    // Can't happen
                    var response = request.CreateResponse(HttpStatusCode.Conflict);
                    return response;
                },
                () =>
                {
                    return request.CreateResponse(HttpStatusCode.NotFound)
                        .AddReason($"The provided loginId [{loginId}] was not found");
                },
                (why) =>
                {
                    return request.CreateResponse(HttpStatusCode.ServiceUnavailable)
                        .AddReason(why);
                });
            return result;
        }
    }
}

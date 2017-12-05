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
using BlackBarLabs.Extensions;

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
            public bool AccountEnabled;
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

                    var baseUrl = url.GetLocation(typeof(Controllers.ActAsUserController));

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
                                        Link = baseUrl.AddParameter("ActorId", info.LoginId.ToString())
                                            .AddParameter("redirectUri",redirectUri)
                                            .AddParameter("token",token)
                                            .ToString(),
                                        ActorId = info.ActorId,
                                        AccountEnabled = info.AccountEnabled
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
            var result = await await context.Sessions.LookupCredentialMappingAsync(
                CredentialValidationMethodTypes.Password, "", loginId, Guid.NewGuid(),
                new Dictionary<string, string>(),
                async (authorizationId, tken, refreshToken, extraParams) =>
                {
                    return await Library.configurationManager.GetRedirectUriAsync(context, CredentialValidationMethodTypes.Password,
                        AuthenticationActions.signin,
                        authorizationId, tken, refreshToken, extraParams, default(Uri),
                        (redirectUrl) =>
                        {
                            var host = request.RequestUri.Host;
                            if (Uri.TryCreate(redirectBase, UriKind.Absolute, out Uri userUrl) && userUrl.Host == "localhost")
                            {
                                var builder = new UriBuilder(redirectUrl)
                                {
                                    Scheme = userUrl.Scheme,
                                    Host = userUrl.Host,
                                    Port = userUrl.Port
                                };
                                redirectUrl = builder.Uri;
                                host = redirectUrl.Host;
                            }
                            var response = request.CreateHtmlResponse($"<script>window.location=\"{redirectUrl}\"</script>");
                            var cookie = new System.Net.Http.Headers.CookieHeaderValue(Api.Constants.Cookies.FakingId, token)
                            {
                                Expires = DateTimeOffset.Now.AddDays(1),
                                Domain = host,
                                Path = "/"
                            };
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
                    return response.ToTask();
                },
                () =>
                {
                    return request.CreateResponse(HttpStatusCode.NotFound)
                        .AddReason($"The provided loginId [{loginId}] was not found")
                        .ToTask();
                },
                (why) =>
                {
                    return request.CreateResponse(HttpStatusCode.ServiceUnavailable)
                        .AddReason(why).ToTask();
                });
            return result;
        }
    }
}

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
using EastFive.Collections.Generic;
using Microsoft.ApplicationInsights;
using EastFive.Linq.Async;
using BlackBarLabs.Linq.Async;

namespace EastFive.Security.SessionServer.Api
{
    public static class ActAsUserActions
    {
        public static async Task<HttpResponseMessage> GetAsync(this Resources.Queries.ActAsUserQuery query,
            HttpRequestMessage request, UrlHelper urlHelper, Func<string, RedirectResult> redirect)
        {
            return await query.ParseAsync(request,
                q => QueryByActorAsync(q.RedirectUri.ParamValue(), q.ActorId.ParamSingle(), request, urlHelper),
                // q => QueryByTokenAsync(q.RedirectUri.ParamValue(), q.Token.ParamValue(), request, urlHelper, redirect),
                q => QueryAllActorsAsync(q.RedirectUri.ParamValue(), q.Token.ParamValue(), request, urlHelper),
                q => QueryAllActorsAsync(q.RedirectUri.ParamValue(), request, urlHelper));
        }
        
        public static async Task<HttpResponseMessage> QueryAllActorsAsync(string redirectUri,
            HttpRequestMessage request, UrlHelper url)
        {
            return await request.GetActorIdClaimsAsync(
                (actorId, claims) => QueryAllActorsAsync(redirectUri, actorId, claims, request, url));
        }

        public static async Task<HttpResponseMessage> QueryAllActorsAsync(string redirectUri, string token,
            HttpRequestMessage request, UrlHelper url)
        {
            return await request.GetActorIdClaimsFromTokenAsync(token,
                (actorId, claims) => QueryAllActorsAsync(redirectUri, actorId, claims, request, url));
        }

        public static async Task<HttpResponseMessage> QueryAllActorsAsync(string redirectUri, Guid actorPerforming, System.Security.Claims.Claim [] claims,
            HttpRequestMessage request, UrlHelper url)
        {
            var context = request.GetSessionServerContext();
            return await await context.PasswordCredentials.GetAllLoginInfoAsync(
                async loginInfos =>
                {
                    var userInfos = await loginInfos
                        .WhereAsync(info => Library.configurationManager.CanAdministerCredentialAsync(info.ActorId, actorPerforming, claims))
                        .SelectAsync(
                            info =>
                            {
                                var userInfo = new Resources.UserInfo
                                {
                                    UserId = info.UserId,
                                    ActorId = info.ActorId,
                                    AccountEnabled = info.AccountEnabled,
                                    Link = info.Tokens
                                        .Append("RedirectUri", redirectUri)
                                        .Aggregate(
                                            url.GetLocation(ServiceConfiguration.credentialProviders[info.Method].CallbackController),
                                            (baseUrl, param) => baseUrl.AddParameter(param.Key, param.Value),
                                            (uri) => uri.AbsoluteUri),
                                };
                                return userInfo;
                            })
                        .ToArrayAsync();

                    if (request.Headers.Accept.Where(accept => accept.MediaType == "application/json").Any())
                        return request.CreateResponse(HttpStatusCode.OK, userInfos);

                    var html = GenerateActAsUserHtml(userInfos);
                    return request.CreateHtmlResponse(html);
                },
                () => request.CreateResponse(HttpStatusCode.ServiceUnavailable)
                    .AddReason("No token providers configured")
                    .ToTask());
        }

        private static string GenerateActAsUserHtml(Resources.UserInfo[] userInfos)
        {
            var tableContents = "";
            foreach (var userInfo in userInfos)
            {
                tableContents += $"<tr><td><a href=\"{userInfo.Link}\">Username: {userInfo.UserId}</a></td></tr>\n";
            }
            var html = $"<html><body><table><tr><th>UserId</th></tr>{tableContents}</table></body></html>";
            return html;
        }

        private static async Task<HttpResponseMessage> QueryByActorAsync(string redirectString, Guid actorId,
            HttpRequestMessage request, UrlHelper url)
        {
            return await request.GetActorIdClaimsAsync(
                async (actorPerforming, claims) =>
                {
                    if (!await Library.configurationManager.CanAdministerCredentialAsync(actorId, actorPerforming, claims))
                        return request.CreateResponse(HttpStatusCode.Unauthorized, $"Actor [{actorPerforming}] cannot administer [{actorId}]");

                    var context = request.GetSessionServerContext();
                    var authenticationRequestId = Guid.NewGuid();
                    var redirectUrl = new Uri(redirectString);
                    var method = ServiceConfiguration.credentialProviders.First().Value.Method;
                    return await await context.Sessions.CreateLoginAsync(authenticationRequestId, actorId,
                            method, redirectUrl,
                        async (session) =>
                        {
                            var config = Library.configurationManager;
                            var redirectResponse = await config.GetRedirectUriAsync(context,
                                    method, AuthenticationActions.signin,
                                    authenticationRequestId, actorId, session.token, session.refreshToken, session.extraParams,
                                    redirectUrl,
                                (redirectUrlSelected) => request.CreateRedirectResponse(redirectUrlSelected),
                                (paramName, why) =>
                                {
                                    var message = $"Invalid parameter while completing login: {paramName} - {why}";
                                    return request.CreateResponse(HttpStatusCode.BadRequest, message).AddReason(why);
                                },
                                (why) =>
                                {
                                    var message = $"General failure while completing login: {why}";
                                    return request.CreateResponse(HttpStatusCode.BadRequest, message)
                                        .AddReason(why);
                                });
                            return redirectResponse;
                        },
                        "Guid not unique for creation of authentication request id".AsFunctionException<Task<HttpResponseMessage>>(),
                        (why) => request.CreateResponseConfiguration("", why).ToTask());
                });
        }

        //private static async Task<HttpResponseMessage> QueryByTokenAndActorIdAsync(string redirectBase, string token,
        //    HttpRequestMessage request, UrlHelper url,
        //    Func<string, RedirectResult> redirect)
        //{
        //    var context = request.GetSessionServerContext();
        //    var result = await await context.Sessions.CreateLoginAsync(Guid.NewGuid(),
        //        CredentialValidationMethodTypes.Password, "", loginId, Guid.NewGuid(),
        //        new Dictionary<string, string>(),
        //        async (authorizationId, tken, refreshToken, extraParams) =>
        //        {
        //            return await Library.configurationManager.GetRedirectUriAsync(context, CredentialValidationMethodTypes.Password,
        //                AuthenticationActions.signin,
        //                loginId,
        //                authorizationId, tken, refreshToken, extraParams, default(Uri),
        //                (redirectUrl) =>
        //                {
        //                    var host = request.RequestUri.Host;
        //                    if (Uri.TryCreate(redirectBase, UriKind.Absolute, out Uri userUrl) && userUrl.Host == "localhost")
        //                    {
        //                        var builder = new UriBuilder(redirectUrl)
        //                        {
        //                            Scheme = userUrl.Scheme,
        //                            Host = userUrl.Host,
        //                            Port = userUrl.Port
        //                        };
        //                        redirectUrl = builder.Uri;
        //                        host = redirectUrl.Host;
        //                    }
        //                    var response = request.CreateHtmlResponse($"<script>window.location=\"{redirectUrl}\"</script>");
        //                    var cookie = new System.Net.Http.Headers.CookieHeaderValue(Api.Constants.Cookies.FakingId, token)
        //                    {
        //                        Expires = DateTimeOffset.Now.AddDays(1),
        //                        Domain = host,
        //                        Path = "/"
        //                    };
        //                    response.Headers.AddCookies(new System.Net.Http.Headers.CookieHeaderValue[] { cookie });
        //                    return response;
        //                },
        //                // TODO: Add reasons
        //                (param, why) => request.CreateResponse(HttpStatusCode.Conflict),
        //                (why) => request.CreateResponse(HttpStatusCode.Conflict));
        //        },
        //        () =>
        //        {
        //            // Can't happen
        //            var response = request.CreateResponse(HttpStatusCode.Conflict);
        //            return response.ToTask();
        //        },
        //        () =>
        //        {
        //            return request.CreateResponse(HttpStatusCode.NotFound)
        //                .AddReason($"The provided loginId [{loginId}] did not map to a user in this system.")
        //                .ToTask();
        //        },
        //        (why) =>
        //        {
        //            return request.CreateResponse(HttpStatusCode.ServiceUnavailable)
        //                .AddReason(why).ToTask();
        //        });
        //    return result;
        //}
    }
}

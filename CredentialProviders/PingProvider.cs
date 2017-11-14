using BlackBarLabs.Extensions;
using EastFive.Security.CredentialProvider;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using EastFive.Security.SessionServer.Persistence;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;

namespace EastFive.Security.SessionServer.CredentialProvider.Ping
{
    public class PingProvider : IProvideCredentials
    {
        private DataContext dataContext;

        public PingProvider(Persistence.DataContext context)
        {
            this.dataContext = context;
        }
        
        
        private static string GetTokenServiceUrl(string pingConnectToken)
        {
            return $"https://sso.connect.pingidentity.com/sso/TXS/2.0/1/{pingConnectToken}";
            //return "https://sso.connect.pingidentity.com/sso/TXS/2.0/2/" + pingConnectToken;
        }

        public async Task<TResult> RedeemTokenAsync<TResult>(string token, Dictionary<string, string> extraParams,
            Func<Guid, IDictionary<string, string>, TResult> onSuccess,
            Func<string, TResult> onInvalidCredentials,
            Func<TResult> onAuthIdNotFound,
            Func<string, TResult> onCouldNotConnect,
            Func<string, TResult> onUnspecifiedConfiguration,
            Func<string, TResult> onFailure)
        {
            var tokenSplit = token.Split(new char[] { ':' });
            var tokenId = tokenSplit[0];
            var agentId = tokenSplit[1];
            return await Web.Configuration.Settings.GetString<Task<TResult>>(Configuration.AppSettings.PingIdentityAthenaRestApiKey,
                async (restApiKey) =>
                {
                    //restApiKey = "tJsb,j1m"; // TODO: Read this and next from config
                    return await Web.Configuration.Settings.GetString(Configuration.AppSettings.PingIdentityAthenaRestAuthUsername,
                        async (restAuthUsername) =>
                        {
                            //restAuthUsername = "c0b42256-3d73-43a9-9a3b-d5b1d47aa08a";
                            using (var httpClient = new HttpClient())
                            {
                                var credentials = Encoding.ASCII.GetBytes($"{restAuthUsername}:{restApiKey}");
                                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentials));
                                var tokenUrl = GetTokenServiceUrl(tokenId);
                                var request = new HttpRequestMessage(
                                    new HttpMethod("GET"), tokenUrl);
                                request.Headers.Add("Cookie", "agentid=" + agentId);
                                //request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain;charset=utf-8");
                                var response = await httpClient.SendAsync(request);
                                var content = await response.Content.ReadAsStringAsync();
                                if (response.StatusCode == HttpStatusCode.OK)
                                {
                                    dynamic stuff = null;
                                    stuff = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(content);
                                    string subject = (string)stuff["pingone.subject"];
                                    //string subject = stuff.pingone.subject;
                                    var hash = SHA512.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(subject));
                                    var id = new Guid(hash.Take(16).ToArray());
                                    var extraParamsWithTokenValues = new Dictionary<string, string>(extraParams);
                                    foreach (var item in stuff)
                                    {
                                        extraParamsWithTokenValues.Add(item.Key.ToString(), item.Value.ToString());
                                    }
                                    return onSuccess(id, extraParamsWithTokenValues);
                                }
                                else
                                {
                                    return onFailure(content);
                                }
                            }
                        },
                        (why) => onUnspecifiedConfiguration(why).ToTask());
                },
                (why) => onUnspecifiedConfiguration(why).ToTask());
        }
    }
}

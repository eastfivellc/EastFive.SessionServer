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
using EastFive.Api.Services;
using System.Security.Claims;
using EastFive.Security.SessionServer;
using System.Collections;

namespace EastFive.Api.Azure.Credentials
{
    [Attributes.IntegrationName(PingProvider.integrationName)]
    public class PingProvider : IProvideLogin
    {
        public const string integrationName = "Ping";

        public const string TokenId = "tokenid";
        public const string AgentId = "agentid";
        public const string Subject = "pingone.subject";
        public const string LastName = "lastName";
        public const string FirstName = "firstName";
        public const string Email = "email";
        public const string PracticeId = "practiceID";
        public const string DepartmentId = "departmentID";
        public const string PatientId = "patientID"; 



        public PingProvider()
        {
        }
        
        private static string GetTokenServiceUrl(string pingConnectToken)
        {
            return $"https://sso.connect.pingidentity.com/sso/TXS/2.0/1/{pingConnectToken}";
            //return "https://sso.connect.pingidentity.com/sso/TXS/2.0/2/" + pingConnectToken;
        }

        [Attributes.IntegrationName(PingProvider.integrationName)]
        public static Task<TResult> InitializeAsync<TResult>(
            Func<IProvideAuthorization, TResult> onProvideAuthorization,
            Func<TResult> onProvideNothing,
            Func<string, TResult> onFailure)
        {
            return onProvideAuthorization(new PingProvider()).ToTask();
        }

        public CredentialValidationMethodTypes Method => CredentialValidationMethodTypes.Ping;

        public Type CallbackController => typeof(Controllers.PingResponseController);

        public async Task<TResult> RedeemTokenAsync<TResult>(IDictionary<string, string> extraParams,
            Func<string, Guid?, Guid?, IDictionary<string, string>, TResult> onSuccess,
            Func<Guid?, IDictionary<string, string>, TResult> onUnauthenticated,
            Func<string, TResult> onInvalidCredentials,
            Func<string, TResult> onCouldNotConnect,
            Func<string, TResult> onUnspecifiedConfiguration,
            Func<string, TResult> onFailure)
        {
            if (!extraParams.ContainsKey(PingProvider.TokenId))
                return onInvalidCredentials("Token Id was not provided");
            if (!extraParams.ContainsKey(PingProvider.AgentId))
                return onInvalidCredentials("AgentId was not provided");
            var tokenId = extraParams[PingProvider.TokenId];
            var agentId = extraParams[PingProvider.AgentId];
            return await Web.Configuration.Settings.GetString<Task<TResult>>(Security.SessionServer.Configuration.AppSettings.PingIdentityAthenaRestApiKey,
                async (restApiKey) =>
                {
                    return await Web.Configuration.Settings.GetString(Security.SessionServer.Configuration.AppSettings.PingIdentityAthenaRestAuthUsername,
                        async (restAuthUsername) =>
                        {
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
                                    var loginId = new Guid(hash.Take(16).ToArray());
                                    loginId = Guid.NewGuid();  //KDH - Take out
                                    var extraParamsWithTokenValues = new Dictionary<string, string>(extraParams);
                                    foreach (var item in stuff)
                                    {
                                        extraParamsWithTokenValues.Add(item.Key.ToString(), item.Value.ToString());
                                    }
                                    return onSuccess(subject, default(Guid?), loginId, extraParamsWithTokenValues);
                                }
                                else
                                {
                                    return onFailure($"{content} TokenId: {tokenId}, AgentId: {agentId}");
                                }
                            }
                        },
                        (why) => onUnspecifiedConfiguration(why).ToTask());
                },
                (why) => onUnspecifiedConfiguration(why).ToTask());
        }
        
        public TResult ParseCredentailParameters<TResult>(IDictionary<string, string> responseParams, 
            Func<string, Guid?, Guid?, TResult> onSuccess, 
            Func<string, TResult> onFailure)
        {
            string subject = responseParams["pingone.subject"];
            var hash = SHA512.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(subject));
            var loginId = new Guid(hash.Take(16).ToArray());

            return onSuccess(subject, default(Guid?), loginId);
        }

        #region IProvideLogin

        public Uri GetLogoutUrl(Guid state, Uri responseControllerLocation, Func<Type, Uri> controllerToLocation)
        {
            return default(Uri);
        }

        public Uri GetLoginUrl(Guid state, Uri responseControllerLocation, Func<Type, Uri> controllerToLocation)
        {
            return default(Uri);
        }

        public Uri GetSignupUrl(Guid state, Uri responseControllerLocation, Func<Type, Uri> controllerToLocation)
        {
            return default(Uri);
        }

        public Task<TResult> UserParametersAsync<TResult>(Guid actorId, System.Security.Claims.Claim[] claims, 
                IDictionary<string, string> extraParams, 
            Func<IDictionary<string, string>, IDictionary<string, Type>, IDictionary<string, string>, TResult> onSuccess)
        {
            return onSuccess(
                new Dictionary<string, string>() { { "push_pmp_file_to_ehr", "Push PMP file to EHR" } },
                new Dictionary<string, Type>() { { "push_pmp_file_to_ehr", typeof(bool) } },
                new Dictionary<string, string>() { { "push_pmp_file_to_ehr", "When true, the system will push PMP files into the provider's clinical documents in their EHR system." } }).ToTask();
        }

        #endregion

    }
}

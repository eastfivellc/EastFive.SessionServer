using EastFive.Security.SessionServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Azure.Credentials
{
    class NoActionAuthorizationRequestManager : IManageAuthorizationRequests
    {
        public async Task<TResult> CredentialValidation<TResult>(string method, 
            IDictionary<string, string> values, 
            Func<TResult> onContinue,
            Func<string, TResult> onStop)
        {
            return onContinue();
        }

        public async Task<TResult> CreatedAuthenticationLoginAsync<TResult>(AzureApplication application,
            Guid sessionId, Guid authorizationId, 
            string token, string refreshToken,
            string method, AuthenticationActions action, IProvideAuthorization provider,
            IDictionary<string, string> extraParams, Uri redirectUrl,
            Func<Task<TResult>> onContinue,
            Func<string, TResult> onStop)
        {
            //var updatingAuthLogTask = saveAuthLogAsync(true, $"Login:{authorizationId}/{sessionId}[{action}]", extraParams);
            application.Telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - Created Authentication.  Creating response.");
            var resp = onContinue();
            //await updatingAuthLogTask;
            return await resp;
        }

        public async Task<TResult> CreatedAuthenticationLogoutAsync<TResult>(AzureApplication application,
                string reason, string method,
                IProvideAuthorization provider, IDictionary<string, string> extraParams, Uri redirectUrl,
            Func<TResult> onContinue,
            Func<string, TResult> onStop)
        {
            //await saveAuthLogAsync(true, $"Logout:{location} -- {reason}", extraParams);
            application.Telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - location: {redirectUrl.AbsolutePath}");

            return onContinue();
        }

        public async Task<TResult> CredentialUnmappedAsync<TResult>(AzureApplication application, 
            string subject, string method,
            IProvideAuthorization credentialProvider, IDictionary<string, string> extraParams,
            Func<Guid, Func<Guid, string, string, AuthenticationActions, Uri, Task<Task<TResult>>>, 
                Func<string, Task<TResult>>, Task<Task<TResult>>> createMappingAsync, 
            Func<TResult> onContinue, 
            Func<string, TResult> onStop)
        {
            application.Telemetry.TrackEvent($"ResponseController.ProcessRequestAsync - Token is not connected to a user in this system.");
            //var updatingAuthLogTask = saveAuthLogAsync(true, $"Login:{subject}/{credentialProvider.GetType().FullName}", extraParams);

            return onContinue();
        }

        public Task<TResult> CredentialUnmappedAsync<TResult>(AzureApplication application,
            string subject, string method, 
            IProvideAuthorization credentialProvider, IDictionary<string, string> extraParams,
            Func<Guid,
                Func<Guid, string, string, AuthenticationActions, Uri, Task<Task<TResult>>>, 
                Func<string, Task<TResult>>,
                Task<Task<TResult>>> createMappingAsync, 
            Func<Task<TResult>> onContinue,
            Func<string, TResult> onStop)
        {
            return onContinue();
        }
    }
}

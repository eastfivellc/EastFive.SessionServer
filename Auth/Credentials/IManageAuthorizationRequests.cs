﻿using EastFive.Azure.Auth;
using EastFive.Security.SessionServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Azure.Credentials
{
    public interface IManageAuthorizationRequests
    {
        Task<TResult> CredentialValidation<TResult>(Guid requestId,
                AzureApplication application,
                IRef<EastFive.Azure.Auth.Method> method, IDictionary<string, string> values,
            Func<Task<TResult>> onContinue,
            Func<string, TResult> onStop);

        Task<TResult> CreatedAuthenticationLoginAsync<TResult>(Guid requestId,
                EastFive.Api.Azure.AzureApplication application,
                Guid sessionId, Guid authorizationId,
                string token, string refreshToken,
                string method, AuthenticationActions action, IProvideAuthorization provider,
                IDictionary<string, string> extraParams, Uri redirectUrl,
            Func<Task<TResult>> onContinue,
            Func<string, TResult> onStop);

        Task<TResult> CreatedAuthenticationLogoutAsync<TResult>(Guid requestId, 
                AzureApplication application, 
                string reason, string method, 
                IProvideAuthorization provider, IDictionary<string, string> extraParams, 
                Uri redirectUrl,
            Func<Task<TResult>> onContinue,
            Func<string, TResult> onStop);

        Task<TResult> CredentialUnmappedAsync<TResult>(Guid requestId,
                AzureApplication application, 
                string subject, string method, IProvideAuthorization credentialProvider, 
                IDictionary<string, string> extraParams,
                Func<Guid,
                    Func<Guid, string, string, AuthenticationActions, Uri, Task<Task<TResult>>>, 
                    Func<string, Task<TResult>>, Task<Task<TResult>>> createMappingAsync, 
            Func<
                Func<Guid,
                    Func<Guid, string, string, AuthenticationActions, Uri, Task<Task<TResult>>>,
                    Func<string, Task<TResult>>,
                    Task<Task<TResult>>>,
                Task<TResult>> onContinue, 
            Func<string, TResult> onStop);

    }
}

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EastFive.Api.Azure.Credentials
{
    public interface IProvideRedirection
    {
        Task<TResult> GetRedirectUriAsync<TResult>(
                Guid accountId, IDictionary<string, string> authParams,
                EastFive.Azure.Auth.Method method, EastFive.Azure.Auth.Authorization authorization,
                Uri baseUri,
                EastFive.Api.Azure.AzureApplication application,
            Func<Uri, TResult> onSuccess,
            Func<TResult> onIgnored,
            Func<string, string, TResult> onInvalidParameter,
            Func<string, TResult> onFailure);
    }
}

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EastFive.Api.Azure.Credentials
{
    public interface IProvideRedirection
    {
        Task<TResult> GetRedirectUriAsync<TResult>(EastFive.Api.Azure.AzureApplication application,
                Guid? authorizationId, Guid requestId, string token, string refreshToken,
                IDictionary<string, string> authorizationParameters,
            Func<Uri, TResult> onSuccess,
            Func<TResult> onIgnored,
            Func<string, string, TResult> onInvalidParameter,
            Func<string, TResult> onFailure);
    }
}

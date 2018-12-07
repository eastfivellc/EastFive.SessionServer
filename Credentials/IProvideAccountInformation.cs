using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using EastFive.Security.SessionServer;

namespace EastFive.Api.Azure.Credentials
{
    public interface IProvideAccountInformation
    {
        Task<TResult> CreateAccount<TResult>(EastFive.Api.Azure.AzureApplication webApiApplication, 
                string method, IProvideAuthorization authorizationProvider, string subject, IDictionary<string, string> extraParameters,
            Func<Guid, TResult> onCreatedMapping,
            Func<TResult> onNoChange);
    }
}

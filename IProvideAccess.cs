using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http.Results;

namespace EastFive.Security.SessionServer
{
    public interface IProvideAccess
    {
        Task<TResult> CreateSessionAsync<TResult>(IDictionary<string, string> parameters,
            Func<HttpClient, IDictionary<string, string>, TResult> onCreatedSession,
            Func<string, TResult> onFailedToCreateSession);
        
    }
}

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http.Results;

namespace EastFive.Security.SessionServer
{
    public interface IProvideToken : IProvideAuthorization
    {
        IDictionary<string, string> CreateTokens(Guid actorId);
    }
}

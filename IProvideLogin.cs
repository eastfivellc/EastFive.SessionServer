using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http.Results;

namespace EastFive.Security.SessionServer
{
    public interface IProvideLogin : IProvideAuthorization
    {
        Type CallbackController { get; }

        Uri GetLoginUrl(Guid state, Uri responseControllerLocation);

        Uri GetLogoutUrl(Guid state, Uri responseControllerLocation);
        
        Uri GetSignupUrl(Guid state, Uri responseControllerLocation);
        
    }
}

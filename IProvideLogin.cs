using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EastFive.Security.SessionServer
{
    public interface IProvideLogin : IProvideAuthorization
    {
        Uri GetLoginUrl(Guid state, Uri responseControllerLocation);

        Uri GetLoginUrl(string redirect_uri, byte mode, byte[] state, Uri responseControllerLocation);
        Uri GetSignupUrl(string redirect_uri, byte mode, byte[] state, Uri responseControllerLocation);
        Uri GetLogoutUrl(string redirect_uri, byte mode, byte[] state, Uri responseControllerLocation);

        TResult ParseState<TResult>(string state,
            Func<byte, byte[], IDictionary<string, string>, TResult> onSuccess,
            Func<string, TResult> invalidState);
        
    }
}

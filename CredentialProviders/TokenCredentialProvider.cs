using BlackBarLabs.Extensions;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EastFive.Security.CredentialProvider.Token
{
    public class TokenCredentialProvider : IProvideCredentials
    {
        private SessionServer.Persistence.DataContext dataContext;

        public TokenCredentialProvider(SessionServer.Persistence.DataContext dataContext)
        {
            this.dataContext = dataContext;
        }

        public Task<TResult> RedeemTokenAsync<TResult>(string accessToken,
            Func<Guid, IDictionary<string, string>, TResult> onSuccess,
            Func<string, TResult> invalidCredentials, Func<TResult> onAuthIdNotFound, Func<string, TResult> couldNotConnect)
        {
            return this.dataContext.CredentialMappings.FindTokenCredentialByTokenAsync(Guid.Parse(accessToken),
                (inviteId, actorId, loginId) =>
                {
                    if (!loginId.HasValue)
                        return invalidCredentials("Token is not connected to an account");

                    return onSuccess(loginId.Value, null);
                },
                () => invalidCredentials("Token does not exist"));
        }
    }
}

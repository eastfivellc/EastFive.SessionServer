using System;
using System.Threading.Tasks;
using BlackBarLabs.Security.AuthorizationServer.Exceptions;
using BlackBarLabs.Security.Authorization;
using System.Configuration;
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using BlackBarLabs.Collections.Async;

namespace BlackBarLabs.Security.AuthorizationServer
{
    public class Claims
    {
        private Context context;
        private SessionServer.Persistence.IDataContext dataContext;

        public async Task<TResult> FindAsync<TResult>(Guid authorizationId, Uri type,
            Func<IEnumerableAsync<Func<Guid, Guid, Uri, string, Task>>, TResult> found,
            Func<TResult> authorizationNotFound,
            Func<string, TResult> failure)
        {
            return await this.dataContext.Authorizations.UpdateClaims(authorizationId,
                (claimsStored, addClaim) =>
                {
                    var claims = EnumerableAsync.YieldAsync<Func<Guid, Guid, Uri, string, Task>>(
                        async (yieldAsync) =>
                        {
                            foreach(var claim in claimsStored)
                            {
                                    if (default(Uri) == type ||
                                        String.Compare(type.AbsoluteUri, claim.type.AbsoluteUri) == 0)
                                    {
                                        await yieldAsync(claim.claimId, authorizationId, claim.type, claim.value);
                                    }
                            }
                        });
                    return Task.FromResult(found(claims));
                },
                // TODO: Create and use dataContext.Authorizations.FindClaims since next two methods are mute since addClaim is never invoked
                () => true,
                () => false,
                () => authorizationNotFound(),
                (whyFailed) => failure(whyFailed));
        }

        internal Claims(Context context, SessionServer.Persistence.IDataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }

        public async Task<TResult> CreateAsync<TResult>(Guid claimId,
            Guid authorizationId, Uri issuer, Uri type, string value, string signature,
            Func<TResult> success,
            Func<TResult> authorizationNotFound,
            Func<TResult> alreadyExist,
            Func<string, TResult> failure)
        {
            return await this.dataContext.Authorizations.UpdateClaims<TResult, bool>(authorizationId,
                async (claimsStored, addClaim) =>
                {
                    if (claimsStored.Any(claim => claim.claimId == claimId))
                        return alreadyExist();

                    var successAddingClaim = await addClaim(claimId, issuer, type, value);
                    if (successAddingClaim)
                        return success();

                    return failure("Could not add claim");
                },
                () => true,
                () => false,
                () => authorizationNotFound(),
                (whyFailed) => failure(whyFailed));
        }

        public async Task<TResult> UpdateAsync<TResult>(Guid claimId,
            Guid authorizationId, Uri issuer, Uri type, string value, string signature,
            Func<TResult> success,
            Func<TResult> authorizationNotFound,
            Func<TResult> claimNotFound,
            Func<string, TResult> failure)
        {
            return await this.dataContext.Authorizations.UpdateClaims<TResult, bool>(authorizationId,
                async (claimsStored, saveClaim) =>
                {
                    if (!claimsStored.Any(claim => claim.claimId == claimId))
                        return claimNotFound();

                    var successAddingClaim = await saveClaim(claimId, issuer, type, value);
                    if (successAddingClaim)
                        return success();

                    return failure("Could not add claim");
                },
                () => true,
                () => false,
                () => authorizationNotFound(),
                (whyFailed) => failure(whyFailed));
        }
    }
}

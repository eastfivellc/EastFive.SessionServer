using System;
using System.Threading.Tasks;
using System.Linq;

using BlackBarLabs.Collections.Async;
using BlackBarLabs.Extensions;
using EastFive.Security.SessionServer.Persistence.Documents;

namespace EastFive.Security.SessionServer
{
    public class Claims
    {
        private Context context;
        private Persistence.DataContext dataContext;

        internal Claims(Context context, Persistence.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }

        public async Task<TResult> FindByAccountIdAsync<TResult>(Guid accountId,
            Func<System.Security.Claims.Claim [], TResult> found,
            Func<TResult> accountNotFound,
            Func<string, TResult> failure)
        {
            throw new NotImplementedException();
            //return await this.dataContext.Authorizations.UpdateClaims(authorizationId,
            //    (claimsStored, addClaim) =>
            //    {
            //        var claims = EnumerableAsync.YieldAsync<Func<Guid, Guid, Uri, string, Task>>(
            //            async (yieldAsync) =>
            //            {
            //                foreach (var claim in claimsStored)
            //                {
            //                    if (default(Uri) == type ||
            //                        String.Compare(type.AbsoluteUri, claim.type.AbsoluteUri) == 0)
            //                    {
            //                        await yieldAsync(claim.claimId, authorizationId, claim.type, claim.value);
            //                    }
            //                }
            //            });
            //        return Task.FromResult(found(claims));
            //    },
            //    // TODO: Create and use dataContext.Authorizations.FindClaims since next two methods are mute since addClaim is never invoked
            //    () => true,
            //    () => false,
            //    () => authorizationNotFound(),
            //    (whyFailed) => failure(whyFailed));
        }

        //internal Claims(Context context, SessionServer.Persistence.Azure.DataContext dataContext)
        //{
        //    this.dataContext = dataContext;
        //    this.context = context;
        //}

        public async Task<TResult> CreateOrUpdateAsync<TResult>(Guid claimId,
            Guid accountId, string type, string value,
            Func<TResult> success,
            Func<TResult> onAccountNotFound,
            Func<TResult> alreadyExist,
            Func<string, TResult> failure)
        {
            throw new NotImplementedException();
            //return await this.dataContext.UpdateClaims<TResult, bool>(accountId,
            //    async (claimsStored, addClaim) =>
            //    {
            //        if (claimsStored.Any(claim => claim.claimId == claimId))
            //            return alreadyExist();

            //        var successAddingClaim = await addClaim(claimId, issuer, type, value);
            //        if (successAddingClaim)
            //            return success();

            //        return failure("Could not add claim");
            //    },
            //    () => true,
            //    () => false,
            //    () => authorizationNotFound(),
            //    (whyFailed) => failure(whyFailed));
        }

        public async Task<TResult> CreateOrUpdateAsync<TResult>(Guid actorId, Guid claimId, string type, string value,
            Func<TResult> onSuccess,
            Func<TResult> onFailure,
            Func<TResult> onActorNotFound)
        {
            return await dataContext.Claims.CreateOrUpdateAsync(actorId, claimId, type, value,
                onSuccess,
                onFailure,
                onActorNotFound);
        }

        //public async Task<TResult> UpdateAsync<TResult>(Guid claimId,
        //    Guid authorizationId, Uri issuer, Uri type, string value, string signature,
        //    Func<TResult> success,
        //    Func<TResult> authorizationNotFound,
        //    Func<TResult> claimNotFound,
        //    Func<string, TResult> failure)
        //{
        //    return await this.dataContext.Authorizations.UpdateClaims<TResult, bool>(authorizationId,
        //        async (claimsStored, saveClaim) =>
        //        {
        //            if (!claimsStored.Any(claim => claim.claimId == claimId))
        //                return claimNotFound();

        //            var successAddingClaim = await saveClaim(claimId, issuer, type, value);
        //            if (successAddingClaim)
        //                return success();

        //            return failure("Could not add claim");
        //        },
        //        () => true,
        //        () => false,
        //        () => authorizationNotFound(),
        //        (whyFailed) => failure(whyFailed));
        //}
    }
}

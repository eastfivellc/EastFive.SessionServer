using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlackBarLabs.Security.SessionServer.Persistence
{
    public delegate Task<bool> CredentialProviderDelegate(Uri providerId, string username, Uri [] externalClaimsLocations);
    public delegate Task<bool> ShouldCreateCallback();

    public struct Claim
    {
        public Guid claimId;
        public Uri issuer;
        public Uri type;
        public string value;
    }

    public delegate Task<TResult> SaveClaimDelegate<TResult>(Guid claimId, Uri issuer, Uri type, string value);
    public delegate Task<TResult> UpdateClaimsSuccessDelegateAsync<TResult, TResultAdded>(Claim[] claims,
        SaveClaimDelegate<TResultAdded> addClaim);

    public interface IAuthorizations
    {
        /// <summary>
        /// For a given set of parameters, see if there is a set of credential information that matches.
        /// </summary>
        /// <param name="authorizationId"></param>
        /// <param name="providerId"></param>
        /// <param name="userId"></param>
        /// <returns>True, if a match is found; false, if match was found but did not match; false, if match was not found;</returns>
        Task<bool> DoesMatchAsync(Guid authorizationId, Uri providerId, string userId);

        /// <summary>
        /// For the provided providerId and userId, find the associated authentication id
        /// </summary>
        /// <param name="providerId"></param>
        /// <param name="userId"></param>
        Task<T> FindAuthId<T>(Uri providerId, string username,
            Func<Guid, Claim[], T> onSuccess, Func<T> onFailure);

        Task<TResult> UpdateClaims<TResult, TResultAdded>(Guid authorizationId,
            UpdateClaimsSuccessDelegateAsync<TResult, TResultAdded> onSuccess,
            Func<TResultAdded> addedSuccess,
            Func<TResultAdded> addedFailure,
            Func<TResult> notFound,
            Func<string, TResult> failure);

        Task<TResult> CreateCredentialProviderAsync<TResult>(Guid authorizationId, Uri providerId, string username,
            Func<TResult> success, Func<TResult> authorizationDoesNotExists, Func<Guid, TResult> alreadyAssociated);

        Task<TResult> UpdateCredentialTokenAsync<TResult>(Guid authorizationId, Uri providerId, string username, Uri[] claimsProviders,
                Func<TResult> success, Func<TResult> authorizationDoesNotExists, Func<Guid, TResult> alreadyAssociated);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="authorizationId"></param>
        /// <param name="createCredentialProviderDelegateCallback">Will be invoked until it return null,
        /// is expected to invoke the delegate each time it is called to add a credential set to the authorization.</param>
        /// <returns></returns>
        Task<T> CreateAuthorizationAsync<T>(Guid authorizationId,
            Func<T> onSuccess, Func<T> onAlreadyExist);
    }
}

using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BlackBarLabs.Collections.Async;
using BlackBarLabs.Persistence.Azure;
using BlackBarLabs.Persistence.Azure.StorageTables;
using BlackBarLabs.Core.Extensions;

namespace BlackBarLabs.Security.SessionServer.Persistence.Azure
{
    internal class Authorizations : IAuthorizations
    {
        private AzureStorageRepository repository;
        public Authorizations(AzureStorageRepository repository)
        {
            this.repository = repository;
        }

        private Guid GetRowKey(Uri providerId, string username)
        {
            var concatination = providerId.AbsoluteUri + username;
            var md5 = MD5.Create();
            // Convert the input string to a byte array and compute the hash. 
            byte[] data = md5.ComputeHash(Encoding.UTF8.GetBytes(concatination));

            var rowId = new Guid(data);
            var md5Hash = GetMd5Hash(md5, concatination);
            return md5Hash;
        }

        public async Task<T> FindAuthId<T>(Uri providerId, string username,
            Func<Guid, Claim[], T> onSuccess, Func<T> onFailure)
        {
            var authCheckId = GetRowKey(providerId, username);
            var result = await await repository.FindByIdAsync(authCheckId,
                async (Documents.AuthorizationCheck document) =>
                {
                    return await await repository.FindByIdAsync(document.AuthId,
                        async (Documents.AuthorizationDocument authorizationDocument) =>
                        {
                            var claims = await authorizationDocument.GetClaims(repository);
                            return onSuccess(document.AuthId, claims);
                        },
                        () =>
                        {
                            // TODO: Log data inconsistency exception
                            return onFailure().ToTask();
                        });
                },
                () => Task.FromResult(onFailure()));
            return result;
        }

        public async Task<bool> DoesMatchAsync(Guid authorizationId, Uri providerId, string username)
        {
            var md5Hash = GetRowKey(providerId, username);
            var result = await repository.FindByIdAsync(md5Hash,
                (Documents.AuthorizationCheck doc) =>
                {
                    return doc.AuthId == authorizationId;
                },
                () => false);
            return result;
        }

        public Task<TResult> UpdateCredentialTokenAsync<TResult>(Guid authorizationId, Uri providerId, string username, Uri[] claimsProviders,
            Func<TResult> success, Func<TResult> authorizationDoesNotExists, Func<Guid, TResult> alreadyAssociated)
        {
            throw new NotImplementedException();
        }

        public async Task<T> CreateAuthorizationAsync<T>(Guid authorizationId, Func<T> onSuccess, Func<T> onAlreadyExist)
        {
            var authorizationDocument = new Documents.AuthorizationDocument()
            {
                RowKey = authorizationId.AsRowKey(),
                PartitionKey = authorizationId.AsRowKey().GeneratePartitionKey(),
            };

            return await repository.CreateAsync(authorizationId, authorizationDocument,
                () => onSuccess(),
                () => onAlreadyExist());
        }

        static Guid GetMd5Hash(MD5 md5Hash, string input)
        {

            // Convert the input string to a byte array and compute the hash. 
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            return new Guid(data);
        }

        public async Task<TResult> CreateCredentialProviderAsync<TResult>(Guid authorizationId, Uri providerId, string username,
            Func<TResult> success, Func<TResult> authorizationDoesNotExists, Func<Guid, TResult> alreadyAssociated)
        {
            return await await repository.FindByIdAsync(authorizationId,
                async (Documents.AuthorizationDocument authorizationStored) =>
                {
                    var authorizationCheckId = GetRowKey(providerId, username);
                    var authorizationDocument = new Documents.AuthorizationCheck
                    {
                        AuthId = authorizationId,
                    };
                    return await await await repository.CreateAsync(authorizationCheckId, authorizationDocument,
                        () => Task.FromResult(Task.FromResult(success())),
                        () =>
                        {
                            return repository.FindByIdAsync(authorizationCheckId,
                                (Documents.AuthorizationCheck authorizationCheckDocument) =>
                                    Task.FromResult(alreadyAssociated(authorizationCheckDocument.AuthId)),
                                () => CreateCredentialProviderAsync(authorizationId, providerId, username,
                                        success, authorizationDoesNotExists, alreadyAssociated));
                        });
                },
                () => Task.FromResult(authorizationDoesNotExists()));
        }

        public async Task<TResult> UpdateClaims<TResult, TResultAdded>(Guid authorizationId,
            UpdateClaimsSuccessDelegateAsync<TResult, TResultAdded> onSuccess,
            Func<TResultAdded> addedSuccess,
            Func<TResultAdded> addedFailure,
            Func<TResult> notFound,
            Func<string, TResult> failure)
        {
            return await repository.UpdateAsync<Documents.AuthorizationDocument, TResult>(authorizationId,
                async (authorizationDocument, save) =>
                {
                    var claims = await authorizationDocument.GetClaims(repository);
                    var result = await onSuccess(claims,
                        async (claimId, issuer, type, value) =>
                        {
                            var claimDoc = new Documents.ClaimDocument()
                            {
                                ClaimId = claimId,
                                Issuer = issuer == default(Uri) ? default(string) : issuer.AbsoluteUri,
                                Type = type == default(Uri) ? default(string) : type.AbsoluteUri,
                                Value = value,
                            };

                            return await await authorizationDocument.AddClaimsAsync(claimDoc, repository,
                                async () =>
                                {
                                    await save(authorizationDocument);
                                    return addedSuccess();
                                },
                                () => Task.FromResult(addedFailure()));
                        });
                    return result;
                },
                () => notFound());
        }
    }
}
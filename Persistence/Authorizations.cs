using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BlackBarLabs.Collections.Async;
using BlackBarLabs.Persistence.Azure;
using BlackBarLabs.Persistence.Azure.StorageTables;
using BlackBarLabs.Extensions;
using BlackBarLabs.Persistence;
using BlackBarLabs.Persistence.Azure.Extensions;
using System.Linq;
using BlackBarLabs.Linq;
using BlackBarLabs;
using System.Collections.Generic;

namespace EastFive.Security.SessionServer.Persistence.Azure
{
    public struct AuthorizationProvider
    {
        public string userId;
        public CredentialValidationMethodTypes method;
        public Uri provider;
    }

    public class Authorizations
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
        
        public Task<TResult> UpdateCredentialTokenAsync<TResult>(Guid authorizationId, Uri providerId, string username, Uri[] claimsProviders,
            Func<TResult> success, Func<TResult> authorizationDoesNotExists, Func<Guid, TResult> alreadyAssociated)
        {
            throw new NotImplementedException();
        }

        static Guid GetMd5Hash(MD5 md5Hash, string input)
        {

            // Convert the input string to a byte array and compute the hash. 
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            return new Guid(data);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <typeparam name="TResultAdded"></typeparam>
        /// <param name="authorizationId"></param>
        /// <param name="onSuccess">claims, (claimId,issuer,type,value)</param>
        /// <param name="addedSuccess"></param>
        /// <param name="addedFailure"></param>
        /// <param name="notFound"></param>
        /// <param name="failure"></param>
        /// <returns></returns>
        public async Task<TResult> UpdateClaims<TResult, TResultAdded>(Guid authorizationId,
            Func<Claim[], Func<Guid, Uri, Uri, string, Task<TResultAdded>>, Task<TResult>> onSuccess,
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
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
using System.IdentityModel;
using System.Net.Http;
using EastFive.Security.SessionServer.Persistence.Documents;

namespace EastFive.Security.SessionServer.Persistence
{
    public class Accesses
    {
        private AzureStorageRepository repository;

        public Accesses(AzureStorageRepository repository)
        {
            this.repository = repository;
        }

        public async Task<TResult> CreateAsync<TResult>(Guid accountId, 
                CredentialValidationMethodTypes method, IDictionary<string, string> paramSet,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyExists)
        {
            var methodName = Enum.GetName(typeof(CredentialValidationMethodTypes), method);
            var doc = new AccessDocument
            {
                Method = methodName,
            };
            doc.SetExtraParams(paramSet);
            return await repository.CreateAsync(accountId, methodName, doc,
                () => onSuccess(),
                () => onAlreadyExists());
        }

        public async Task<TResult> FindAsync<TResult>(Guid actorId, CredentialValidationMethodTypes method,
            Func<IDictionary<string, string>, TResult> found,
            Func<TResult> actorNotFound)
        {
            var methodName = Enum.GetName(typeof(CredentialValidationMethodTypes), method);
            var results = await repository.FindByIdAsync(actorId, methodName,
                (AccessDocument doc) => found(doc.GetExtraParams()),
                () => actorNotFound());
            return results;
        }
    }
}
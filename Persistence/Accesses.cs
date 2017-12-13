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
using EastFive.Serialization;

namespace EastFive.Security.SessionServer.Persistence
{
    public class Accesses
    {
        private AzureStorageRepository repository;

        public Accesses(AzureStorageRepository repository)
        {
            this.repository = repository;
        }

        public async Task<TResult> CreateAsync<TResult>(Guid integrationId, Guid accountId, 
                CredentialValidationMethodTypes method, IDictionary<string, string> paramSet,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyExists)
        {
            var methodName = Enum.GetName(typeof(CredentialValidationMethodTypes), method);
            var docByMethod = new AccessDocument
            {
                LookupId = integrationId,
                Method = methodName,
            };
            docByMethod.SetExtraParams(paramSet);
            var docById = new AccessDocument
            {
                LookupId = accountId,
                Method = methodName,
            };
            docById.SetExtraParams(paramSet);
            var rollback = new RollbackAsync<TResult>();
            rollback.AddTaskCreate(accountId, methodName, docByMethod, onAlreadyExists, this.repository);
            rollback.AddTaskCreate(integrationId, docById, onAlreadyExists, this.repository);
            return await rollback.ExecuteAsync(onSuccess);
        }

        public async Task<TResult> FindAsync<TResult>(Guid actorId, CredentialValidationMethodTypes method,
            Func<Guid, IDictionary<string, string>, TResult> found,
            Func<TResult> actorNotFound)
        {
            var methodName = Enum.GetName(typeof(CredentialValidationMethodTypes), method);
            var results = await repository.FindByIdAsync(actorId, methodName,
                (AccessDocument doc) => found(doc.LookupId, doc.GetExtraParams()),
                () => actorNotFound());
            return results;
        }

        public async Task<TResult> FindUpdatableAsync<TResult>(Guid actorId, CredentialValidationMethodTypes method,
            Func<Guid, IDictionary<string, string>, Func<IDictionary<string, string>, Task>, Task<TResult>> onFound,
            Func<TResult> actorNotFound)
        {
            var methodName = Enum.GetName(typeof(CredentialValidationMethodTypes), method);
            var results = await repository.UpdateAsync<AccessDocument, TResult>(actorId, methodName,
                async (doc, saveAsync) =>
                {
                    return await onFound(doc.LookupId, doc.GetExtraParams(),
                        async (extraParamsNew) =>
                        {
                            doc.SetExtraParams(extraParamsNew);
                            await saveAsync(doc);
                        });
                },
                () => actorNotFound());
            return results;
        }

        public async Task<TResult> DeleteAsync<TResult>(Guid accessId,
            Func<TResult> onDeleted,
            Func<TResult> actorNotFound)
        {
            var results = await repository.DeleteIfAsync<AccessDocument, TResult>(accessId,
                async (doc, deleteAsync) =>
                {
                    await deleteAsync();
                    return await repository.DeleteIfAsync<AccessDocument, TResult>(doc.LookupId, doc.Method,
                        async (lookupDoc, deleteLookupAsync) =>
                        {
                            await deleteLookupAsync();
                            return onDeleted();
                        },
                        () => onDeleted());
                },
                () => actorNotFound());
            return results;
        }

        public async Task<TResult> DeleteAsync<TResult>(Guid actorId, CredentialValidationMethodTypes method,
            Func<TResult> onDeleted,
            Func<TResult> actorNotFound)
        {
            var methodName = Enum.GetName(typeof(CredentialValidationMethodTypes), method);
            var results = await repository.DeleteIfAsync<AccessDocument, TResult>(actorId, methodName,
                async (doc, deleteAsync) =>
                {
                    await deleteAsync();
                    return await repository.DeleteIfAsync<AccessDocument, TResult>(doc.LookupId,
                        async (lookupDoc, deleteLookupAsync) =>
                        {
                            await deleteLookupAsync();
                            return onDeleted();
                        },
                        () => onDeleted());
                },
                () => actorNotFound());
            return results;
        }
    }
}
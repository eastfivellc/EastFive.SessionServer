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
                CredentialValidationMethodTypes method,// IDictionary<string, string> paramSet,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyExists)
        {
            var methodName = Enum.GetName(typeof(CredentialValidationMethodTypes), method);
            var docByMethod = new AccessDocument
            {
                LookupId = integrationId,
                Method = methodName,
            };
            //docByMethod.SetExtraParams(paramSet);
            var docById = new AccessDocument
            {
                LookupId = accountId,
                Method = methodName,
            };
            //docById.SetExtraParams(paramSet);
            var rollback = new RollbackAsync<TResult>();
            rollback.AddTaskCreate(accountId, methodName, docByMethod, onAlreadyExists, this.repository);
            rollback.AddTaskCreate(integrationId, docById, onAlreadyExists, this.repository);
            return await rollback.ExecuteAsync(onSuccess);
        }

        public async Task<TResult> FindAsync<TResult>(Guid actorId, CredentialValidationMethodTypes method,
            Func<Guid, TResult> found,
            Func<TResult> actorNotFound)
        {
            var methodName = Enum.GetName(typeof(CredentialValidationMethodTypes), method);
            var results = await repository.FindByIdAsync(actorId, methodName,
                (AccessDocument doc) => found(doc.LookupId),
                () => actorNotFound());
            return results;
        }

        internal async Task<TResult> FindUpdatableAsync<TResult>(Guid actorId, CredentialValidationMethodTypes method,
            Func<Guid, IDictionary<string, string>, Func<IDictionary<string, string>, Task>, Task<TResult>> onFound,
            Func<Func<Guid, IDictionary<string, string>, Task<Guid>>, Task<TResult>> onNotFound)
        {
            var methodName = Enum.GetName(typeof(CredentialValidationMethodTypes), method);
            var results = await await repository.UpdateAsync<AccessDocument, Task<TResult>>(actorId, methodName,
                (doc, saveAsync) =>
                {
                    return onFound(doc.LookupId, doc.GetExtraParams(),
                        (extraParamsNew) =>
                        {
                            doc.SetExtraParams(extraParamsNew);
                            return saveAsync(doc);
                        }).ToTask();
                },
                () =>
                {
                    return onNotFound(
                        (integrationId, extraParams) =>
                        {
                            return this.CreateAsync(integrationId, actorId, method, //extraParams,
                                () => integrationId,
                                "Guid not unique".AsFunctionException<Guid>());
                        });
                });
            return results;
        }

        public async Task<TResult> DeleteAsync<TResult>(Guid accessId,
            Func<CredentialValidationMethodTypes, IDictionary<string, string>, TResult> onDeleted,
            Func<TResult> actorNotFound)
        {
            var results = await repository.DeleteIfAsync<AccessDocument, TResult>(accessId,
                async (doc, deleteAsync) =>
                {
                    await deleteAsync();
                    Enum.TryParse(doc.Method, out CredentialValidationMethodTypes method);
                    return await repository.DeleteIfAsync<AccessDocument, TResult>(doc.LookupId, doc.Method,
                        async (lookupDoc, deleteLookupAsync) =>
                        {
                            await deleteLookupAsync();
                            return onDeleted(method, doc.GetExtraParams());
                        },
                        () => onDeleted(method, doc.GetExtraParams()));
                },
                () => actorNotFound());
            return results;
        }

        public async Task<TResult> DeleteAsync<TResult>(Guid actorId, CredentialValidationMethodTypes method,
            Func<CredentialValidationMethodTypes, IDictionary<string, string>, TResult> onDeleted,
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
                            return onDeleted(method, doc.GetExtraParams());
                        },
                        () => onDeleted(method, doc.GetExtraParams()));
                },
                () => actorNotFound());
            return results;
        }
    }
}
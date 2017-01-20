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
    public struct CredentialRedirect
    {

    }

    public class CredentialRedirects
    {
        private AzureStorageRepository repository;
        public CredentialRedirects(AzureStorageRepository repository)
        {
            this.repository = repository;
        }
        
        public async Task<TResult> CreateCredentialMappingAsync<TResult>(Guid loginId, Guid actorId,
            Func<TResult> success,
            Func<TResult> onAlreadyExists)
        {
            var document = new Documents.CredentialMappingDocument
            {
                AuthId = actorId,
            };
            return await repository.CreateAsync(loginId, document,
                () => success(),
                () => onAlreadyExists());
        }

        public async Task<TResult> LookupCredentialMappingAsync<TResult>(Guid loginId,
            Func<Guid, TResult> onSuccess,
            Func<TResult> onNotExist)
        {
            return await repository.FindByIdAsync(loginId,
                (Documents.CredentialMappingDocument document) => onSuccess(document.AuthId),
                () => onNotExist());
        }

        internal Task<TResult> CreateCredentialRedirectTokenAsync<TResult>(Guid redirectId,
            Guid actorId, string email,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyAssociated)
        {
            var rollback = new RollbackAsync<TResult>();
            var credentialRedirectDoc = new Documents.CredentialRedirectDocument()
            {
                ActorId = actorId,
                Email = email,
                IsToken = true,
            };
            rollback.AddTaskCreate(redirectId, credentialRedirectDoc, onAlreadyAssociated, this.repository);
            rollback.AddTaskCreateOrUpdate(actorId,
                (Documents.AuthorizationDocument doc) => doc.AddRedirect(redirectId),
                (doc) => doc.RemoveRedirect(redirectId),
                onAlreadyAssociated,
                this.repository);
            return rollback.ExecuteAsync(onSuccess);
        }

        internal Task<TResult> CreateCredentialRedirectAsync<TResult>(Guid redirectId,
            Guid actorId, string email,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyAssociated)
        {
            var rollback = new RollbackAsync<TResult>();
            var credentialRedirectDoc = new Documents.CredentialRedirectDocument()
            {
                ActorId = actorId,
                Email = email,
                IsToken = false,
            };
            rollback.AddTaskCreate(redirectId, credentialRedirectDoc, onAlreadyAssociated, this.repository);
            rollback.AddTaskCreateOrUpdate(actorId,
                (Documents.AuthorizationDocument doc) =>
                {
                    if (!doc.AddRedirect(redirectId))
                        return false;
                    return doc.AddAssociatedEmail(email);
                },
                (doc) => doc.RemoveRedirect(redirectId) || doc.RemoveAssociatedEmail(email),
                onAlreadyAssociated,
                this.repository);
            return rollback.ExecuteAsync(onSuccess);
        }

        internal Task<TResult> FindCredentialRedirectAsync<TResult>(Guid redirectId,
            Func<bool, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return repository.FindByIdAsync(redirectId,
                (Documents.CredentialRedirectDocument document) => onFound(document.Redeemed),
                () => onNotFound());
        }

        internal async Task<TResult> MarkCredentialRedirectAsync<TResult>(Guid redirectId,
            Func<Guid, TResult> onFound,
            Func<TResult> onNotFound,
            Func<Guid, TResult> onAlreadyRedeemed)
        {
            var lookupResults = await repository.UpdateAsync<Documents.CredentialRedirectDocument, KeyValuePair<Guid, bool>?>(redirectId,
                async (document, update) =>
                {
                    if (document.Redeemed)
                        return document.ActorId.PairWithValue(true);
                    document.Redeemed = true;
                    await update(document);
                    return document.ActorId.PairWithValue(false);
                },
                () => default(KeyValuePair<Guid, bool>?));
            if (!lookupResults.HasValue)
                return onNotFound();
            if (lookupResults.Value.Value)
                return onAlreadyRedeemed(lookupResults.Value.Key);
            return onFound(lookupResults.Value.Key);
        }

        internal Task<TResult> FindCredentialRedirectsByAuthIdAsync<TResult>(Guid authId,
            Func<CredentialRedirect, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return repository.FindByIdAsync(redirectId,
                (Documents.CredentialRedirectDocument document) => onFound(document.Redeemed),
                () => onNotFound());
        }
    }
}
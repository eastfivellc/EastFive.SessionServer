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
using System.Net.Http;

namespace EastFive.Security.SessionServer.Persistence
{
    public class CredentialMappings
    {
        private AzureStorageRepository repository;
        public CredentialMappings(AzureStorageRepository repository)
        {
            this.repository = repository;
        }
        
        public async Task<TResult> LookupCredentialMappingAsync<TResult>(Guid loginId,
            Func<Guid, TResult> onSuccess,
            Func<TResult> onNotExist)
        {
            return await repository.FindByIdAsync(loginId,
                (Documents.LoginActorLookupDocument document) => onSuccess(document.ActorId),
                () => onNotExist());
        }
        
        internal async Task<TResult> CreateInviteAsync<TResult>(Guid inviteId,
            Guid actorId, string email, Guid token, DateTime lastSent, bool isToken,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyExists)
        {
            var rollback = new RollbackAsync<TResult>();

            var inviteDocument = new Documents.InviteDocument()
            {
                ActorId = actorId,
                Email = email,
                IsToken = isToken,
                LastSent = lastSent,
                Token = token,
            };
            rollback.AddTaskCreate(inviteId, inviteDocument, onAlreadyExists, this.repository);

            var inviteTokenDocument = new Documents.InviteTokenDocument()
            {
                ActorId = actorId,
                InviteId = inviteId,
            };
            rollback.AddTaskCreate(token, inviteTokenDocument, onAlreadyExists, this.repository);

            rollback.AddTaskCreateOrUpdate(actorId,
                (Documents.ActorMappingsDocument authDoc) => authDoc.AddInviteId(inviteId),
                (authDoc) => authDoc.RemoveInviteId(inviteId),
                onAlreadyExists, // This should fail on the action above as well
                this.repository);

            return await rollback.ExecuteAsync(onSuccess);
        }

        internal Task<TResult> UpdateTokenCredentialAsync<TResult>(Guid tokenCredentialId,
            Func<string, DateTime?, Guid, Func<string, DateTime?, Task>, Task<TResult>> onFound,
            Func<TResult> onNotFound)
        {
            return this.repository.UpdateAsync<Documents.InviteDocument, TResult>(tokenCredentialId,
                async (currentDoc, saveAsync) =>
                {
                    return await onFound(currentDoc.Email, currentDoc.LastSent, currentDoc.Token,
                        async (email, lastSent) =>
                        {
                            currentDoc.Email = email;
                            currentDoc.LastSent = lastSent;
                            await saveAsync(currentDoc);
                        });
                },
                () => onNotFound());
        }

        internal Task<TResult> FindInviteAsync<TResult>(Guid inviteId, bool isToken,
            Func<Guid, string, DateTime?, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return repository.FindByIdAsync(inviteId,
                (Documents.InviteDocument document) =>
                {
                    if (isToken != document.IsToken)
                        return onNotFound();
                    return onFound(document.ActorId, document.Email, document.LastSent);
                },
                () => onNotFound());
        }
        
        internal async Task<TResult> FindInviteByTokenAsync<TResult>(Guid token,
            Func<Guid, Guid, Guid?, TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return await await repository.FindByIdAsync(token,
                (Documents.InviteTokenDocument document) =>
                    repository.FindByIdAsync(document.InviteId,
                        (Documents.InviteDocument inviteDoc) =>
                        {
                            if (inviteDoc.IsToken)
                                return onNotFound();
                            return onSuccess(document.InviteId, inviteDoc.ActorId, inviteDoc.LoginId);
                        },
                        () =>
                        {
                            // TODO: Log data inconsistency
                            return onNotFound();
                        }),
                () => onNotFound().ToTask());
        }

        internal async Task<TResult> FindTokenCredentialByTokenAsync<TResult>(Guid token,
            Func<Guid, Guid, TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return await await repository.FindByIdAsync(token,
                (Documents.InviteTokenDocument document) =>
                    repository.FindByIdAsync(document.InviteId,
                        (Documents.InviteDocument inviteDoc) =>
                        {
                            if (!inviteDoc.IsToken)
                                return onNotFound();
                            return onSuccess(document.InviteId, inviteDoc.ActorId);
                        },
                        () =>
                        {
                            // TODO: Log data inconsistency
                            return onNotFound();
                        }),
                () => onNotFound().ToTask());
        }

        internal async Task<TResult> FindInviteByActorAsync<TResult>(Guid actorId, bool isToken,
            Func<Invite[], TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return await repository.FindLinkedDocumentsAsync(actorId,
                (document) => document.GetInviteIds(),
                (Documents.ActorMappingsDocument authDoc, Documents.InviteDocument[] inviteDocs) =>
                {
                    var invites = inviteDocs.Where(doc => doc.IsToken == isToken).Select(Convert).ToArray();
                    return onSuccess(invites);
                },
                () => onNotFound());
        }
        
        private static Invite Convert(Documents.InviteDocument inviteDoc)
        {
            return new Invite
            {
                id = inviteDoc.Id,
                actorId = inviteDoc.ActorId,
                email = inviteDoc.Email,
                lastSent = inviteDoc.LastSent,
            };
        }

        internal async Task<TResult> MarkInviteRedeemedAsync<TResult>(Guid inviteToken, Guid loginId,
            Func<Guid, TResult> onFound,
            Func<TResult> onNotFound,
            Func<Guid, TResult> onAlreadyRedeemed,
            Func<TResult> onAlreadyInUse,
            Func<TResult> onAlreadyConnected)
        {
            var lookupResults = await await repository.FindByIdAsync(inviteToken,
                async (Documents.InviteTokenDocument tokenDocument) =>
                {
                    var rollback = new RollbackAsync<TResult>();

                    rollback.AddTaskUpdate(tokenDocument.InviteId,
                        (Documents.InviteDocument inviteDoc) => { inviteDoc.LoginId = loginId; },
                        (inviteDoc) => { inviteDoc.LoginId = default(Guid?); },
                        onNotFound,
                        this.repository);

                    var loginLookup = new Documents.LoginActorLookupDocument()
                    {
                        ActorId = tokenDocument.ActorId,
                    };
                    rollback.AddTaskCreate(loginId, loginLookup, onAlreadyInUse, this.repository);

                    return await rollback.ExecuteAsync(() => onFound(tokenDocument.ActorId));
                },
                () => onNotFound().ToTask());
            return lookupResults;
        }
    }
}
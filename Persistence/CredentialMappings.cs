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
    public struct CredentialMapping
    {
        public Guid id;
        public Guid? actorId;
        public Guid? loginId;
    }

    public class CredentialMappings
    {
        private AzureStorageRepository repository;
        public CredentialMappings(AzureStorageRepository repository)
        {
            this.repository = repository;
        }
        
        public async Task<TResult> CreateCredentialMappingAsync<TResult>(Guid credentialMappingId,
            Guid actorId, Guid? loginId, 
            Func<TResult> onSuccess,
            Func<TResult> onMappingAlreadyExists,
            Func<TResult> onLoginAlreadyUsed)
        {
            var rollback = new RollbackAsync<TResult>();
            var credentialMappingDoc = new Documents.CredentialMappingDocument
            {
                ActorId = actorId,
            };
            rollback.AddTaskCreate(credentialMappingId, credentialMappingDoc,
                onMappingAlreadyExists, this.repository);

            rollback.AddTaskCreateOrUpdate(actorId,
                (Documents.AuthorizationDocument actorDoc) => 
                    actorDoc.AddCredentialMapping(credentialMappingId, loginId),
                actorDoc => actorDoc.RemoveCredentialMapping(credentialMappingId),
                onMappingAlreadyExists,
                this.repository);

            if (loginId.HasValue)
            {
                var credentialMappingLoginLookupDoc = new Documents.CredentialMappingLoginLookupDocument
                {
                    ActorId = actorId,
                };
                rollback.AddTaskCreate(loginId.Value, credentialMappingLoginLookupDoc,
                    onLoginAlreadyUsed, this.repository);
            }
            return await rollback.ExecuteAsync(onSuccess);
        }

        public async Task<TResult> UpdateLoginIdAsync<TResult>(Guid credentialMappingId,
            Func<CredentialMapping, Func<Guid?, Task>, Task<TResult>> onSuccess,
            Func<TResult> credentialAlreadyExists,
            Func<TResult> credentialMappingNotFound)
        {
            var resultFind = await await this.repository.FindByIdAsync(credentialMappingId,
                async (Documents.CredentialMappingDocument doc) =>
                {
                    var resultUpdate = await this.repository.UpdateAsync<Documents.AuthorizationDocument, TResult>(doc.ActorId,
                        async (actorDoc, updateActorDocAsync) =>
                        {
                            var credentialMappings = actorDoc.GetCredentialMappings();
                            if (!credentialMappings.ContainsKey(credentialMappingId))
                            {
                                // TODO: Log data corruption
                                return credentialMappingNotFound();
                            }
                            var loginId = credentialMappings[credentialMappingId];
                            var result = await onSuccess(
                                new CredentialMapping
                                {
                                    id = credentialMappingId,
                                    actorId = doc.ActorId,
                                    loginId = loginId,
                                },
                                async (newLoginId) =>
                                {
                                    credentialMappings[credentialMappingId] = newLoginId;
                                    actorDoc.SetCredentialMappings(credentialMappings);
                                    var lookupDoc = new Documents.CredentialMappingLoginLookupDocument
                                    {
                                        ActorId = doc.ActorId,
                                    };
                                    // TODO: Figure out how to delete and rollback the lookup doc
                                    if(newLoginId.HasValue)
                                        await this.repository.CreateAsync(newLoginId.Value, lookupDoc,
                                            () => true, () => false);
                                    await updateActorDocAsync(actorDoc);
                                });
                            return result;
                        },
                        () =>
                        {
                            // TODO: Log data corruption
                            return credentialMappingNotFound();
                        });
                    return resultUpdate;
                },
                () => credentialMappingNotFound().ToTask());
            return resultFind;
        }

        public async Task<TResult> LookupCredentialMappingAsync<TResult>(Guid loginId,
            Func<Guid, TResult> onSuccess,
            Func<TResult> onNotExist)
        {
            return await repository.FindByIdAsync(loginId,
                (Documents.CredentialMappingLoginLookupDocument document) => onSuccess(document.ActorId),
                () => onNotExist());
        }

        internal Task<TResult> FindCredentialMappingByAuthIdAsync<TResult>(Guid actorId,
            Func<CredentialMapping[], TResult> onFound,
            Func<TResult> onNotFound)
        {
            return repository.FindByIdAsync(actorId,
                (Documents.AuthorizationDocument document) =>
                {
                    return onFound(document.GetCredentialMappings()
                        .Select(kvp => new CredentialMapping
                        {
                            id = kvp.Key,
                            actorId = actorId,
                            loginId = kvp.Value,
                        }).ToArray());
                },
                () => onNotFound());
        }

        internal Task<TResult> CreateTokenAsync<TResult>(Guid redirectId,
            Guid actorId, string email, Guid token,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyAssociated)
        {
            var rollback = new RollbackAsync<TResult>();
            var credentialRedirectDoc = new Documents.InviteDocument()
            {
                ActorId = actorId,
                //Email = email,
                //IsToken = true,
            };
            rollback.AddTaskCreate(redirectId, credentialRedirectDoc, onAlreadyAssociated, this.repository);
            rollback.AddTaskCreateOrUpdate(actorId,
                (Documents.AuthorizationDocument doc) => doc.AddRedirect(redirectId),
                (doc) => doc.RemoveRedirect(redirectId),
                onAlreadyAssociated,
                this.repository);
            return rollback.ExecuteAsync(onSuccess);
        }

        internal async Task<TResult> CreateInviteAsync<TResult>(Guid inviteId,
            Guid credentialMappingId, string email, Guid token,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyExists,
            Func<TResult> onCredentialMappingNotFound)
        {
            var resultFind = await await this.repository.FindByIdAsync(credentialMappingId,
                async (Documents.CredentialMappingDocument doc) =>
                {
                    var rollback = new RollbackAsync<TResult>();
                    var inviteDocument = new Documents.InviteDocument()
                    {
                        ActorId = doc.ActorId,
                    };
                    rollback.AddTaskCreate(inviteId, inviteDocument, onAlreadyExists, this.repository);
                    var inviteTokenDocument = new Documents.InviteTokenDocument()
                    {
                        ActorId = doc.ActorId,
                        InviteId = inviteId,
                    };
                    rollback.AddTaskCreate(token, inviteTokenDocument, onAlreadyExists, this.repository);
                    return await rollback.ExecuteAsync(onSuccess);
                },
                () => onCredentialMappingNotFound().ToTask());
            return resultFind;
        }

        internal Task<TResult> FindInviteAsync<TResult>(Guid inviteId,
            Func<Guid, Guid?, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return repository.FindByIdAsync(inviteId,
                (Documents.InviteDocument document) => onFound(document.ActorId, document.LoginId),
                () => onNotFound());
        }

        internal async Task<TResult> FindInviteByTokenAsync<TResult>(Guid token,
            Func<Guid, Guid, Guid?, TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return await await repository.FindByIdAsync(token,
                (Documents.InviteTokenDocument document) =>
                    FindInviteAsync(document.InviteId,
                    (actorId, loginId) => onSuccess(document.InviteId, actorId, loginId),
                    () =>
                    {
                        // TODO: Log data inconsistency
                        return onNotFound();
                    }),
                () => onNotFound().ToTask());
        }

        internal async Task<TResult> MarkCredentialRedirectAsync<TResult>(Guid inviteToken, Guid loginId,
            Func<Guid, TResult> onFound,
            Func<TResult> onNotFound,
            Func<Guid, TResult> onAlreadyRedeemed)
        {
            var lookupResults = await await repository.FindByIdAsync(inviteToken,
                async (Documents.InviteTokenDocument tokenDocument) =>
                {
                    return await repository.UpdateAsync<Documents.InviteDocument, TResult>(tokenDocument.InviteId,
                        async (inviteDoc, updateAsync) =>
                        {
                            if (inviteDoc.LoginId.HasValue)
                            {
                                if (loginId != inviteDoc.LoginId.Value)
                                    return onAlreadyRedeemed(inviteDoc.LoginId.Value);

                                return onFound(inviteDoc.ActorId);
                            }
                            inviteDoc.LoginId = loginId;
                            await updateAsync(inviteDoc);
                            return onFound(inviteDoc.ActorId);
                        },
                        () =>
                        {
                            // TODO: LOg data inconsistency
                            return onNotFound();
                        });
                },
                () => onNotFound().ToTask());
            return lookupResults;
        }
    }
}
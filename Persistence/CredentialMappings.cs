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
        public Guid actorId;
        public Guid? loginId;
    }

    public class CredentialMappings
    {
        private AzureStorageRepository repository;
        public CredentialMappings(AzureStorageRepository repository)
        {
            this.repository = repository;
        }
        
        //public async Task<TResult> CreateCredentialMappingAsync<TResult>(Guid credentialMappingId,
        //    Guid actorId, Guid? loginId, 
        //    Func<TResult> onSuccess,
        //    Func<TResult> onMappingAlreadyExists,
        //    Func<TResult> onLoginAlreadyUsed)
        //{
        //    var rollback = new RollbackAsync<TResult>();
        //    var credentialMappingDoc = new Documents.CredentialMappingDocument
        //    {
        //        ActorId = actorId,
        //    };
        //    rollback.AddTaskCreate(credentialMappingId, credentialMappingDoc,
        //        onMappingAlreadyExists, this.repository);

        //    rollback.AddTaskCreateOrUpdate(actorId,
        //        (Documents.AuthorizationDocument actorDoc) => 
        //            actorDoc.AddCredentialMapping(credentialMappingId, loginId),
        //        actorDoc => actorDoc.RemoveCredentialMapping(credentialMappingId),
        //        onMappingAlreadyExists,
        //        this.repository);

        //    if (loginId.HasValue)
        //    {
        //        var credentialMappingLoginLookupDoc = new Documents.CredentialMappingLoginLookupDocument
        //        {
        //            ActorId = actorId,
        //        };
        //        rollback.AddTaskCreate(loginId.Value, credentialMappingLoginLookupDoc,
        //            onLoginAlreadyUsed, this.repository);
        //    }
        //    return await rollback.ExecuteAsync(onSuccess);
        //}

        public async Task<TResult> FindByIdAsync<TResult>(Guid credentialMappingId,
            Func<Guid, Guid?, TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return await repository.FindByIdAsync(credentialMappingId,
                (Documents.CredentialMappingDocument document) =>
                    onSuccess(document.ActorId, document.LoginId),
                () => onNotFound());
        }

        //public async Task<TResult> FindByActorAsync<TResult>(Guid actorId,
        //    Func<CredentialMapping[], TResult> onSuccess,
        //    Func<TResult> onNotFound)
        //{
        //    return await repository.FindByIdAsync(actorId,
        //        (Documents.AuthorizationDocument actorDoc) =>
        //        {
        //            var mappingLogins = actorDoc.GetCredentialMappings();
        //            var mappings = mappingLogins.Select(kvp => new CredentialMapping
        //            {
        //                id = kvp.Key,
        //                actorId = actorId,
        //                loginId = kvp.Value,
        //            }).ToArray();
        //            return onSuccess(mappings);
        //        },
        //        () => onNotFound());
        //}

        public async Task<TResult> CreateLoginIdAsync<TResult>(Guid actorId, Guid loginId,
            Func<TResult> onSuccess,
            Func<TResult> onRelationshipAlreadyExists)
        {
            var lookupDoc = new Documents.LoginActorLookupDocument
            {
                ActorId = actorId,
            };
            var resultUpdate = await this.repository.CreateAsync(loginId, lookupDoc,
                () => onSuccess(),
                () => onRelationshipAlreadyExists());
            return resultUpdate;
        }

        public async Task<TResult> LookupCredentialMappingAsync<TResult>(Guid loginId,
            Func<Guid, TResult> onSuccess,
            Func<TResult> onNotExist)
        {
            return await repository.FindByIdAsync(loginId,
                (Documents.LoginActorLookupDocument document) => onSuccess(document.ActorId),
                () => onNotExist());
        }

        //internal Task<TResult> FindCredentialMappingByAuthIdAsync<TResult>(Guid actorId,
        //    Func<CredentialMapping[], TResult> onFound,
        //    Func<TResult> onNotFound)
        //{
        //    return repository.FindByIdAsync(actorId,
        //        (Documents.AuthorizationDocument document) =>
        //        {
        //            return onFound(document.GetCredentialMappings()
        //                .Select(kvp => new CredentialMapping
        //                {
        //                    id = kvp.Key,
        //                    actorId = actorId,
        //                    loginId = kvp.Value,
        //                }).ToArray());
        //        },
        //        () => onNotFound());
        //}

        //internal Task<TResult> FindCredentialMappingByIdAsync<TResult>(Guid credentialMappingId,
        //    Func<CredentialMapping, TResult> onFound,
        //    Func<TResult> onNotFound)
        //{
        //    return repository.FindByIdAsync(credentialMappingId,
        //        (Documents.CredentialMappingDocument document) =>
        //        {
        //            return onFound(new CredentialMapping
        //            {
        //                id = document.Id,
        //                actorId = document.ActorId,
        //                loginId = document.LoginId,
        //            });
        //        },
        //        () => onNotFound());
        //}

        //internal Task<TResult> CreateTokenAsync<TResult>(Guid inviteId,
        //    Guid actorId, string email, Guid token,
        //    Func<TResult> onSuccess,
        //    Func<TResult> onAlreadyAssociated)
        //{
        //    var rollback = new RollbackAsync<TResult>();
        //    var credentialRedirectDoc = new Documents.InviteDocument()
        //    {
        //        ActorId = actorId,
        //        Email = email,
        //        //IsToken = true,
        //    };
        //    rollback.AddTaskCreate(inviteId, credentialRedirectDoc, onAlreadyAssociated, this.repository);
        //    rollback.AddTaskCreateOrUpdate(actorId,
        //        (Documents.AuthorizationDocument doc) => doc.AddRedirect(inviteId),
        //        (doc) => doc.RemoveRedirect(inviteId),
        //        onAlreadyAssociated,
        //        this.repository);
        //    return rollback.ExecuteAsync(onSuccess);
        //}
        
        internal async Task<TResult> CreateInviteAsync<TResult>(Guid inviteId,
            Guid actorId, string email, Guid token,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyExists,
            Func<TResult> onInviteAlreadySetForActor)
        {
            var rollback = new RollbackAsync<TResult>();

            var inviteDocument = new Documents.InviteDocument()
            {
                ActorId = actorId,
                Email = email,
            };
            rollback.AddTaskCreate(inviteId, inviteDocument, onAlreadyExists, this.repository);

            var inviteTokenDocument = new Documents.InviteTokenDocument()
            {
                ActorId = actorId,
                InviteId = inviteId,
            };
            rollback.AddTaskCreate(token, inviteTokenDocument, onAlreadyExists, this.repository);

            rollback.AddTaskCreateOrUpdate(actorId,
                (Documents.AuthorizationDocument authDoc) => authDoc.AddInviteId(inviteId),
                (authDoc) => authDoc.RemoveInviteId(inviteId),
                onInviteAlreadySetForActor,
                this.repository);

            return await rollback.ExecuteAsync(onSuccess);
        }

        internal Task<TResult> FindInviteAsync<TResult>(Guid inviteId,
            Func<Guid, string, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return repository.FindByIdAsync(inviteId,
                (Documents.InviteDocument document) => onFound(document.ActorId, document.Email),
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
                            return onSuccess(document.InviteId, inviteDoc.ActorId, inviteDoc.LoginId);
                        },
                        () =>
                        {
                            // TODO: Log data inconsistency
                            return onNotFound();
                        }),
                () => onNotFound().ToTask());
        }

        internal async Task<TResult> FindInviteByActorAsync<TResult>(Guid actorId,
            Func<Invite[], TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return await repository.FindLinkedDocumentsAsync(actorId,
                (document) => document.GetInviteIds(),
                (Documents.AuthorizationDocument authDoc, Documents.InviteDocument[] inviteDocs) =>
                {
                    var invites = inviteDocs.Select(Convert).ToArray();
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
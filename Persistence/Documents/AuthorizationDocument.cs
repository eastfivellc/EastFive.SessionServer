using System;
using System.Linq;
using System.Threading.Tasks;

using BlackBarLabs;
using BlackBarLabs.Collections.Async;
using BlackBarLabs.Persistence.Azure;
using BlackBarLabs.Persistence.Azure.StorageTables;
using BlackBarLabs.Linq;
using System.Collections.Generic;

namespace EastFive.Security.SessionServer.Persistence.Azure.Documents
{
    internal class AuthorizationDocument : Microsoft.WindowsAzure.Storage.Table.TableEntity
    {
        #region Constructors

        public AuthorizationDocument() { }

        internal async Task<Claim[]> GetClaims(AzureStorageRepository repository)
        {
            var claimDocumentIds = Claims.ToGuidsFromByteArray();
            var claims = await claimDocumentIds
                .Select(
                     async (claimDocumentId) =>
                     {
                         return await repository.FindByIdAsync(claimDocumentId,
                             (ClaimDocument claimsDoc) =>
                             {
                                 Uri issuer;
                                 Uri.TryCreate(claimsDoc.Issuer, UriKind.RelativeOrAbsolute, out issuer);
                                 Uri type;
                                 Uri.TryCreate(claimsDoc.Type, UriKind.RelativeOrAbsolute, out type);
                                 return new Claim
                                 {
                                     claimId = claimsDoc.ClaimId,
                                     issuer = issuer,
                                     type = type,
                                     value = claimsDoc.Value,
                                 };
                             },
                             () =>
                             {
                                 // TODO: Flag data inconsitency
                                 return default(Claim?);
                             });
                     })
                .WhenAllAsync();
            return claims.Where(claim => claim.HasValue).Select(claim => claim.Value).ToArray();
        }

        public byte [] CredentialMappings { get; set; }

        internal IDictionary<Guid, Guid?> GetCredentialMappings()
        {
            return CredentialMappings.FromByteArray(
                    (mappingBytes) => new Guid(mappingBytes),
                    (loginBytes) => loginBytes.Length == 0 ?
                        default(Guid?) : new Guid(loginBytes));
        }

        internal void SetCredentialMappings(IDictionary<Guid, Guid?> mappings)
        {
            CredentialMappings = mappings.ToByteArray(
                    (actorId) => actorId.ToByteArray(),
                    (loginId) => loginId.HasValue ?
                        loginId.Value.ToByteArray() : new byte[] { });
        }

        internal bool RemoveCredentialMapping(Guid credentialMappingId)
        {
            var mappings = GetCredentialMappings();
            if (!mappings.ContainsKey(credentialMappingId))
                return false;
            mappings.Remove(credentialMappingId);
            SetCredentialMappings(mappings);
            return true;
        }

        internal bool AddCredentialMapping(Guid credentialMappingId, Guid? loginId)
        {
            var mappings = GetCredentialMappings();
            if (mappings.ContainsKey(credentialMappingId))
                return false;
            mappings.Add(credentialMappingId, loginId);
            SetCredentialMappings(mappings);
            return true;
        }

        #endregion

        #region Properties

        public byte [] Claims { get; set; }

        internal async Task<TResult> AddClaimsAsync<TResult>(ClaimDocument claimsDoc, AzureStorageRepository repository,
            Func<TResult> success,
            Func<TResult> failure)
        {
            var claimDocumentIdsCurrent = Claims.ToGuidsFromByteArray();
            var claimDocumentIds = claimDocumentIdsCurrent.Concat(new Guid[] { claimsDoc.ClaimId });
            this.Claims = claimDocumentIds.ToByteArrayOfGuids();
            return await repository.CreateAsync(claimsDoc.ClaimId, claimsDoc,
                        () => success(),
                        () => failure());
        }

        internal void AddProviders(AuthorizationProvider[] authorizationProviders)
        {

        }

        public byte[] Redirects { get; set; }

        internal Guid[] GetClaimMappings()
        {
            return this.Redirects.ToGuidsFromByteArray();
        }

        internal bool AddRedirect(Guid redirectId)
        {
            var redirectIds = this.GetClaimMappings();
            if (redirectIds.Contains(redirectId))
                return false;
            this.Redirects = redirectIds.Append(redirectId).ToByteArrayOfGuids();
            return true;
        }

        internal bool RemoveRedirect(Guid redirectId)
        {
            var redirectIds = this.GetClaimMappings();
            if (!redirectIds.Contains(redirectId))
                return false;
            this.Redirects = redirectIds.Where(rId => rId != redirectId).ToByteArrayOfGuids();
            return true;
        }

        public byte [] InviteIds { get; set; }

        internal bool AddInviteId(Guid inviteId)
        {
            var inviteIds = this.GetInviteIds();
            if (inviteIds.Contains(inviteId))
                return false;
            this.InviteIds = inviteIds.Append(inviteId).ToByteArrayOfGuids();
            return true;
        }

        internal bool RemoveInviteId(Guid inviteId)
        {
            var inviteIds = this.GetInviteIds();
            if (!inviteIds.Contains(inviteId))
                return false;
            this.InviteIds = inviteIds.Where(rId => rId != inviteId).ToByteArrayOfGuids();
            return true;
        }

        internal Guid[] GetInviteIds()
        {
            return this.InviteIds.ToGuidsFromByteArray();
        }

        #endregion

    }
}

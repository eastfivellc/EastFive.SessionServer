using System;
using System.Linq;
using System.Threading.Tasks;

using BlackBarLabs;
using BlackBarLabs.Collections.Async;
using BlackBarLabs.Persistence.Azure;
using BlackBarLabs.Persistence.Azure.StorageTables;
using BlackBarLabs.Linq;

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

        #endregion

        #region Properties

        public byte [] Claims { get; set; }
        public string AssociatedEmails { get; set; }

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

        internal bool AddRedirect(Guid redirectId)
        {
            var redirectIds = this.Redirects.ToGuidsFromByteArray();
            if (redirectIds.Contains(redirectId))
                return false;
            this.Redirects = redirectIds.Append(redirectId).ToByteArrayOfGuids();
            return true;
        }

        internal bool RemoveRedirect(Guid redirectId)
        {
            var redirectIds = this.Redirects.ToGuidsFromByteArray();
            if (!redirectIds.Contains(redirectId))
                return false;
            this.Redirects = redirectIds.Where(rId => rId != redirectId).ToByteArrayOfGuids();
            return true;
        }

        internal bool AddAssociatedEmail(string email)
        {
            var associatedEmailsStorage = this.AssociatedEmails;
            var associatedEmails = String.IsNullOrWhiteSpace(associatedEmailsStorage) ?
                new string[] { }
                :
                associatedEmailsStorage.Split(new[] { ',' });
            if (associatedEmails.Contains(email))
                return false;
            this.AssociatedEmails = associatedEmails.Append(email).Join(",");
            return true;
        }

        internal bool RemoveAssociatedEmail(string email)
        {
            var associatedEmailsNew = this.AssociatedEmails.Split(new[] { ',' })
                        .Where(e => e.CompareTo(email) != 0)
                        .Join(",");
            var updated = associatedEmailsNew.Length != this.AssociatedEmails.Length;
            this.AssociatedEmails = associatedEmailsNew;
            return updated;
        }

        #endregion

    }
}

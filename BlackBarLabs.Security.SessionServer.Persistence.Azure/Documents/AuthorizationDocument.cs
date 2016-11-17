using System;
using System.Linq;
using System.Threading.Tasks;
using BlackBarLabs.Collections.Async;
using BlackBarLabs.Persistence.Azure;
using BlackBarLabs.Persistence.Azure.StorageTables;
using BlackBarLabs.Core;

namespace BlackBarLabs.Security.SessionServer.Persistence.Azure.Documents
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

        #endregion

    }
}

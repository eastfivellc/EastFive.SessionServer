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
using EastFive.Security.SessionServer.Persistence.Documents;

namespace EastFive.Security.SessionServer.Persistence
{
    public class Claims
    {
        private AzureStorageRepository repository;
        public Claims(AzureStorageRepository repository)
        {
            this.repository = repository;
        }

        public async Task<TResult> CreateOrUpdateAsync<TResult>(Guid actorId, Guid claimId, string type, string value,
              Func<TResult> onSuccess,
              Func<TResult> onFailure,
              Func<TResult> onActorNotFound)
        {
            return await await repository.FindByIdAsync(actorId,
                (ActorMappingsDocument document) =>
                {
                    var claimDoc = new ClaimDocument()
                    {
                        ClaimId = claimId,
                        Issuer = actorId.ToString("N"), //TODO - Is this is the correct issuer data???
                        Type = type,
                        Value = value
                    };
                    var result = document.AddOrUpdateClaimsAsync(claimDoc, repository,
                        onSuccess,
                        onFailure);
                    return result;
                },
                () => onActorNotFound().ToTask());
        }
    }
}
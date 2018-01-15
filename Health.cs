using System;
using System.Threading.Tasks;
using System.Linq;

using BlackBarLabs.Collections.Async;
using BlackBarLabs.Extensions;
using EastFive.Security.SessionServer.Persistence.Documents;

namespace EastFive.Security.SessionServer
{
    public class Health
    {
        private Context context;
        private Persistence.DataContext dataContext;

        internal Health(Context context, Persistence.DataContext dataContext)
        {
            this.dataContext = dataContext;
            this.context = context;
        }

        public struct CredentialToActorMappingInfo
        {
            public Guid PasswordCredentialId;
            public Guid PasswordCredentialLoginId;
            public Guid LoginActorLookupActorId;
            public Guid ActorMappingId;
            public bool LoginActorFound;
            public bool ActorMappingFound;
        }

        public async Task<TResult> CheckCredentialToActorMappingAsync<TResult>(
            Func<CredentialToActorMappingInfo[], TResult> onSuccess)
        {
            var result = await await dataContext.PasswordCredentials.FindAllAsync(
                async passwordCredentials =>
                {
                    return await await dataContext.CredentialMappings.FindAllCredentialMappingAsync(
                        async loginActorLookups =>
                        {
                            return await dataContext.CredentialMappings.FindAllActorMappingsAsync(
                                actorMappingIds =>
                                {
                                    return passwordCredentials.Select(
                                        pc =>
                                        {
                                            var loginActorLookupActorId = default(Guid);
                                            var actorMappingId = default(Guid);
                                            var loginActorLookupsMatchingLoginId = loginActorLookups
                                                .Where(l => l.loginId == pc.LoginId);

                                            var loginActorFound = loginActorLookupsMatchingLoginId.Any();
                                            if(loginActorFound)
                                            {
                                                var loginActorLookup = loginActorLookupsMatchingLoginId.First();
                                                loginActorLookupActorId = loginActorLookup.actorId;
                                                actorMappingId = actorMappingIds.FirstOrDefault(id => id == loginActorLookupActorId);
                                            }
                                            
                                            var actorMappingFound = false;
                                            if (default(Guid) != actorMappingId)
                                                actorMappingFound = true;

                                            return new CredentialToActorMappingInfo
                                            {
                                                PasswordCredentialId = pc.Id,
                                                PasswordCredentialLoginId = pc.LoginId,
                                                LoginActorLookupActorId = loginActorLookupActorId,
                                                ActorMappingId = actorMappingId,
                                                LoginActorFound = loginActorFound,
                                                ActorMappingFound = actorMappingFound
                                            };



                                        }).ToArray();
                                });
                        });
                });

            var credsWithIssues = result.Where(info => info.LoginActorFound == false || info.ActorMappingFound == false).ToArray();
            return onSuccess(credsWithIssues);
        }
    }
}

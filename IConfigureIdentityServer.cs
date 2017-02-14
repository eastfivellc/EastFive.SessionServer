using BlackBarLabs.Api.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Routing;
using System.Security.Claims;

namespace EastFive.IdentityServer
{
    public interface IConfigureIdentityServer
    {
        WebId GetActorLink(Guid actorId, UrlHelper urlHelper);

        Task<bool> CanAdministerCredentialAsync(Guid actorInQuestion, Guid actorTakingAction, Claim[] claims);
    }
}

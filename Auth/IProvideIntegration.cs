using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Auth
{
    public interface IProvideIntegration
    {
        string GetDefaultName(IDictionary<string, string> extraParams);

        Task<bool> SupportsIntegrationAsync(Guid accountId);


        [Obsolete("Moving to each login provided have a custom route for configuration")]
        Task<TResult> UserParametersAsync<TResult>(Guid actorId, System.Security.Claims.Claim[] claims, IDictionary<string, string> extraParams,
            Func<
                IDictionary<string, string>, //Key, label
                IDictionary<string, Type>,   //Key, type
                IDictionary<string, string>, //Key, description
                TResult> onSuccess);
    }
}

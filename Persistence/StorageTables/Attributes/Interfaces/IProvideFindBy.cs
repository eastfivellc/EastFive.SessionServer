using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive.Linq.Async;

namespace EastFive.Persistence.Azure.StorageTables
{
    public interface IProvideFindBy
    {
        IEnumerableAsync<KeyValuePair<string, string>> GetKeys<TEntity>(IRef<TEntity> value,
                Driver.AzureTableDriverDynamic repository, MemberInfo memberInfo)
            where TEntity : IReferenceable;
    }

    public interface IProvideFindByAsync
    {
        Task<TResult> GetKeysAsync<TEntity, TResult>(IRef<TEntity> value,
                Driver.AzureTableDriverDynamic repository, MemberInfo memberInfo,
            Func<IEnumerableAsync<KeyValuePair<string, string>>, TResult> onRefFound,
            Func<TResult> onRefNotFound)
            where TEntity : IReferenceable;
    }
}

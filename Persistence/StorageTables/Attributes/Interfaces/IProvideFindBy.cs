using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive.Linq.Async;

namespace EastFive.Persistence.Azure.StorageTables
{
    public interface IProvideFindBy
    {
        IEnumerableAsync<IRefAst> GetKeys(object memberValue,
            MemberInfo memberInfo, Driver.AzureTableDriverDynamic repository,
            KeyValuePair<MemberInfo, object>[] queries);
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

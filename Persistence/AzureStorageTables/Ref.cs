using BlackBarLabs.Persistence.Azure.StorageTables;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Persistence
{
    public class Ref<T> : EastFive.IRef<T>
        where T : struct
    {
        public Ref(Guid id)
        {
            this.id = id;
        }

        public T? value
        {
            get;
            private set;
        }

        public Guid id
        {
            get;
            private set;
        }

        public bool resolved
        {
            get;
            private set;
        }

        public Task<TResult> ValueAsync<TResult>(Func<T, TResult> valueCallback)
        {
            return AzureStorageRepository.Connection(
                connect =>
                {
                    object v = 1;
                    return valueCallback((T)v).AsTask();
                });
        }
    }
}

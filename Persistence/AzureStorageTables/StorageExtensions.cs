using BlackBarLabs;
using EastFive.Async;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Persistence.Azure.StorageTables.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Persistence.AzureStorageTables
{
    public static class StorageExtensions
    {
        public static Task<TResult> AzureStorageTableFindAsync<TEntity, TResult>(this Guid resourceId,
            Func<TEntity, TResult> onFound,
            Func<TResult> onDoesNotExists)
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .FindByIdAsync<TEntity, TResult>(resourceId,
                    onFound,
                    onDoesNotExists);
        }

        public static async Task<ITransactionResult<TResult>> CheckAsync<T, TResult>(this IRef<T> value,
            Func<TResult> onNotFound)
            where T : struct
        {
            if (value.IsDefaultOrNull())
                return onNotFound().TransactionResultFailure();

            await value.ResolveAsync();
            if (!value.value.HasValue)
                return onNotFound().TransactionResultFailure();

            Func<Task> rollback = () => 1.AsTask();
            return rollback.TransactionResultSuccess<TResult>();
        }

        public static IRef<TEntity> IRefStorage<TEntity>(this Guid entityId)
            where TEntity : struct
        {
            return new Ref<TEntity>(entityId);
        }

        public static Task<TResult> AzureStorageTableCreateAsync<TEntity, TResult>(this TEntity entity,
            Func<Guid, TResult> onCreated,
            Func<TResult> onAlreadyExists)
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .CreateAsync(entity,
                    onCreated,
                    onAlreadyExists);
        }

        public static async Task<ITransactionResult<TResult>> AzureStorageTableRollbackCreateAsync<TEntity, TResult>(this TEntity entity,
            Func<TResult> onAlreadyExists)
        {
            var driver = AzureTableDriverDynamic
                .FromSettings();
            return await driver
                .CreateAsync(entity,
                    (resourceId) =>
                    {
                        Func<Task> rollback = (() =>
                        {
                            return driver.DeleteByIdAsync<TEntity, bool>(resourceId,
                                () => true,
                                () => false);
                        });
                        return rollback.TransactionResultSuccess<TResult>();
                    },
                    () => onAlreadyExists().TransactionResultFailure());
        }
    }
}

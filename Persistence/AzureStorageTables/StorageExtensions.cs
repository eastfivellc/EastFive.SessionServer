using EastFive.Extensions;
using EastFive.Persistence.Azure.StorageTables.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Persistence.AzureStorageTables
{
    public interface ITransactionResult
    {
        TResult Execute<TResult>();
    }

    public static class StorageExtensions
    {
        public static async Task<TResult> CheckAsync<T, TResult>(this IRef<T> value,
            Func<TResult> onFound,
            Func<TResult> onNotFound)
            where T : struct
        {
            if (value.IsDefaultOrNull())
                return onNotFound();

            await value.ResolveAsync();
            if (value.value.HasValue)
                return onFound();
            return onNotFound();
        }

        public static Task<TResult> AzureStorageTableCreateAsync<TEntity, TResult>(this TEntity entity,
            Func<TResult> onCreated,
            Func<TResult> onAlreadyExists)
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .CreateAsync(entity,
                    onCreated,
                    onAlreadyExists);
        }

        private class TransactionResultSuccess<TResult> : ITransactionResult
        {
            public Func<TResult> onCompleteSuccess;

            public TransactionResultSuccess(Func<TResult> onCompleteSuccess)
            {
                this.onCompleteSuccess = onCompleteSuccess;
            }

            public TResult1 Execute<TResult1>()
            {
                return (TResult1)((object)onCompleteSuccess());
            }
        }

        public static async Task<TResult> TransactionAsync<T, TResult>(this T objectTransact,
            Func<ITransactionResult, Func<TResult, ITransactionResult>, Task<ITransactionResult>> firstRollback,
            Func<TResult> onCompleteSuccess)
        {
            var success = new TransactionResultSuccess<TResult>(onCompleteSuccess);
            var nextRollback = await firstRollback(success,
                result => new TransactionResultSuccess<TResult>(() => result));
            return nextRollback.Execute<TResult>();
        }

        public static async Task<TResult> TransactionAsync<T, TResult>(this T objectTransact,
            Func<ITransactionResult, Func<TResult, ITransactionResult>, Task<ITransactionResult>> firstRollback,
            Func<ITransactionResult, Func<TResult, ITransactionResult>, Task<ITransactionResult>> secondRollback,
            Func<TResult> onCompleteSuccess)
        {
            var success = new TransactionResultSuccess<TResult>(onCompleteSuccess);
            var nextRollback = await firstRollback(success,
                result => new TransactionResultSuccess<TResult>(() => result));
            var secondRollbackWrapped = await secondRollback(nextRollback,
                result => new TransactionResultSuccess<TResult>(() => result));
            return secondRollbackWrapped.Execute<TResult>();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using EastFive.Async;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Linq.Expressions;
using EastFive.Reflection;
using EastFive.Persistence.Azure.StorageTables.Driver;
using BlackBarLabs.Persistence.Azure.StorageTables;
using BlackBarLabs.Persistence.Azure;
using Microsoft.WindowsAzure.Storage.Table;
using EastFive.Persistence.Azure;
using EastFive.Azure.StorageTables.Driver;

namespace EastFive.Azure.Persistence.AzureStorageTables
{
    public static class StorageExtensions
    {
        public static Task<TResult> StorageGetAsync<TEntity, TResult>(this Guid resourceId,
            Func<TEntity, TResult> onFound,
            Func<TResult> onDoesNotExists = default(Func<TResult>),
            Func<string> getPartitionKey = default(Func<string>))
        {
            if (default == getPartitionKey)
                getPartitionKey = () => resourceId.AsRowKey().GeneratePartitionKey();

            if (default == onDoesNotExists)
                onDoesNotExists = Api.ResourceNotFoundException.StorageGetAsync<TResult>;

            return AzureTableDriverDynamic
                .FromSettings()
                .FindByIdAsync(resourceId.AsRowKey(), getPartitionKey(),
                    onFound,
                    onDoesNotExists);
        }

        public static Task<TResult> StorageGetAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
            Func<TEntity, TResult> onFound,
            Func<TResult> onDoesNotExists = default(Func<TResult>),
            Func<string> getPartitionKey = default(Func<string>))
            where TEntity : struct, IReferenceable
        {
            return StorageGetAsync(entityRef.id,
                onFound,
                onDoesNotExists,
                getPartitionKey);
        }

        public static TResult StorageGetBy<TRefEntity, TEntity, TResult>(this IRef<TRefEntity> entityRef,
                Expression<Func<TEntity, IRef<TRefEntity>>> by,
            Func<IEnumerableAsync<TEntity>, TResult> onFound,
            Func<TResult> onRefNotFound = default(Func<TResult>))
            where TEntity : struct, IReferenceable
            where TRefEntity : struct, IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .FindBy(entityRef,
                        by,
                    onFound,
                    onRefNotFound);
        }

        public static Task<TResult> StorageGetAsync<TEntity, TResult>(this IRefObj<TEntity> entityRefObj,
            Func<TEntity, TResult> onFound,
            Func<TResult> onDoesNotExists = default(Func<TResult>),
            Func<string> getPartitionKey = default(Func<string>))
            where TEntity : class, IReferenceable
        {
            if (default(Func<string>) == getPartitionKey)
                getPartitionKey = () => entityRefObj.id.AsRowKey().GeneratePartitionKey();

            return AzureTableDriverDynamic
                .FromSettings()
                .FindByIdAsync(entityRefObj.id.AsRowKey(), getPartitionKey(),
                    onFound,
                    onDoesNotExists);
        }

        public static Task<TResult> StorageGetAsync<TEntity, TResult>(this IRefOptional<TEntity> entityRefMaybe,
            Func<TEntity, TResult> onFound,
            Func<TResult> onDoesNotExists = default(Func<TResult>),
            Func<string> getPartitionKey = default(Func<string>))
            where TEntity : struct, IReferenceable
        {
            if (!entityRefMaybe.HasValueNotNull())
                return onDoesNotExists().AsTask();

            var entityRef = entityRefMaybe.Ref;
            return StorageGetAsync(entityRef,
                onFound,
                onDoesNotExists,
                getPartitionKey);
        }

        public static IEnumerableAsync<TEntity> StorageGet<TEntity>(this IRefs<TEntity> entityRefs)
            where TEntity : struct, IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .FindByIdsAsync<TEntity>(entityRefs.ids);
        }

        public static IEnumerableAsync<TEntity> StorageQuery<TEntity>(
            this Expression<Func<TEntity, bool>> query)
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .FindAll(query);
        }

        public static IEnumerableAsync<IDictionary<string, TValue>> StorageGetPartitionAsDictionary<TValue>(
            this string partition, string tableName)
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .FindByPartition<EastFive.Persistence.Azure.StorageTables.DictionaryTableEntity<TValue>>(partition,
                    tableName)
                .Select(dictTableEntity => dictTableEntity.values);
        }

        public static Microsoft.WindowsAzure.Storage.Table.ITableEntity ToTableEntity<TValue>(this IDictionary<string, TValue> dictionary,
            string rowKey, string partitionKey)
        {
            return new EastFive.Persistence.Azure.StorageTables.DictionaryTableEntity<TValue>(
                rowKey, partitionKey, dictionary);
        }

        public static Microsoft.WindowsAzure.Storage.Table.ITableEntity ToTableEntity<TValue>(this IDictionary<string, TValue> dictionary,
            Guid id)
        {
            var key = id.AsRowKey();
            var partition = key.GeneratePartitionKey();
            return new EastFive.Persistence.Azure.StorageTables.DictionaryTableEntity<TValue>(
                key, partition, dictionary);
        }

        public static IEnumerableAsync<Microsoft.WindowsAzure.Storage.Table.TableResult> StorageQueryDelete<TEntity>(
            this Expression<Func<TEntity, bool>> query)
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .DeleteAll(query);
        }

        public static Task<TResult> StorageUpdateAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
            Func<TEntity, Func<TEntity, Task>, Task<TResult>> onUpdate,
            Func<TResult> onNotFound = default(Func<TResult>),
            StorageTables.Driver.AzureStorageDriver.RetryDelegateAsync<Task<TResult>> onTimeoutAsync =
                default(StorageTables.Driver.AzureStorageDriver.RetryDelegateAsync<Task<TResult>>))
            where TEntity : struct, IReferenceable
        {
            var documentId = entityRef.id;
            return AzureTableDriverDynamic
                .FromSettings()
                .UpdateAsync(documentId,
                    onUpdate,
                    onNotFound: onNotFound,
                    onTimeoutAsync: onTimeoutAsync);
        }

        public static Task<TResult> StorageCreateOrUpdateAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
            Func<TEntity,TEntity> setId,
            Func<bool, TEntity, Func<TEntity, Task>, Task<TResult>> onCreated,
                Func<string> getPartitionKey = default(Func<string>))
            where TEntity : struct, IReferenceable
        {
            var documentId = entityRef.id;
            return AzureTableDriverDynamic
                .FromSettings()
                .UpdateOrCreateAsync<TEntity, TResult>(documentId,
                    setId,
                    onCreated,
                    default(AzureStorageDriver.RetryDelegateAsync<Task<TResult>>),
                    getPartitionKey);
        }

        public static IEnumerableAsync<TResult> StorageCreateOrUpdateBatch<TEntity, TResult>(this IEnumerable<TEntity> entities,
            Func<TEntity, Microsoft.WindowsAzure.Storage.Table.TableResult, TResult> perItemCallback,
            string tableName = default(string),
            StorageTables.Driver.AzureStorageDriver.RetryDelegate onTimeout =
                    default(StorageTables.Driver.AzureStorageDriver.RetryDelegate),
            EastFive.Analytics.ILogger diagnostics = default(EastFive.Analytics.ILogger))
            where TEntity : class, Microsoft.WindowsAzure.Storage.Table.ITableEntity
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .CreateOrUpdateBatch<TResult>(entities,
                    (tableEntity, result) =>
                    {
                        var entity = tableEntity as TEntity;
                        return perItemCallback(entity, result);
                    },
                    tableName: tableName,
                    onTimeout: onTimeout,
                    diagnostics:diagnostics);
        }

        public static IEnumerableAsync<TResult> StorageCreateOrUpdateBatch<TEntity, TResult>(this IEnumerableAsync<TEntity> entities,
            Func<TEntity, Microsoft.WindowsAzure.Storage.Table.TableResult, TResult> perItemCallback,
            string tableName = default(string),
            StorageTables.Driver.AzureStorageDriver.RetryDelegate onTimeout = 
                    default(StorageTables.Driver.AzureStorageDriver.RetryDelegate),
            EastFive.Analytics.ILogger diagnostics = default(EastFive.Analytics.ILogger))
            where TEntity : class, Microsoft.WindowsAzure.Storage.Table.ITableEntity
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .CreateOrUpdateBatch<TResult>(entities,
                    (tableEntity, result) =>
                    {
                        var entity = tableEntity as TEntity;
                        return perItemCallback(entity, result);
                    },
                    tableName: tableName,
                    onTimeout:onTimeout,
                    diagnostics:diagnostics);
        }

        public static Task<TResult> StorageUpdateAsync<TEntity, TResult>(this IRefObj<TEntity> entityRef,
            Func<TEntity, Func<TEntity, Task>, Task<TResult>> onUpdate,
            Func<TResult> onNotFound = default(Func<TResult>),
            StorageTables.Driver.AzureStorageDriver.RetryDelegateAsync<Task<TResult>> onTimeoutAsync =
                default(StorageTables.Driver.AzureStorageDriver.RetryDelegateAsync<Task<TResult>>))
            where TEntity : class, IReferenceable
        {
            var documentId = entityRef.id;
            return AzureTableDriverDynamic
                .FromSettings()
                .UpdateAsync(documentId,
                    onUpdate,
                    onNotFound: onNotFound,
                    onTimeoutAsync: onTimeoutAsync);
        }

        public static Task<TResult> StorageDeleteAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
            Func<TResult> onSuccess,
            Func<TResult> onNotFound = default(Func<TResult>))
            where TEntity : struct, IReferenceable
        {
            var documentId = entityRef.id;
            return AzureTableDriverDynamic
                .FromSettings()
                .DeleteByIdAsync<TEntity, TResult>(documentId,
                    onSuccess,
                    onNotFound: onNotFound);
        }

        public static IEnumerableAsync<TResult> StorageDeleteBatch<TEntity, TResult>(this IEnumerableAsync<IRef<TEntity>> entityRefs,
            Func<Microsoft.WindowsAzure.Storage.Table.TableResult, TResult> onSuccess)
            where TEntity : struct, IReferenceable
        {
            var documentIds = entityRefs.Select(entity => entity.id);
            return AzureTableDriverDynamic
                .FromSettings()
                .DeleteBatch<TEntity, TResult>(documentIds, onSuccess);
        }
        

        public static Task<TResult> StorageLockedUpdateAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
                Expression<Func<TEntity, DateTime?>> lockedPropertyExpression,
            AzureTableDriverDynamic.WhileLockedDelegateAsync<TEntity, TResult> onLockAquired,
            Func<TResult> onNotFound,
            Func<TResult> onLockRejected = default(Func<TResult>),
            AzureTableDriverDynamic.ContinueAquiringLockDelegateAsync<TEntity, TResult> onAlreadyLocked =
                        default(AzureTableDriverDynamic.ContinueAquiringLockDelegateAsync<TEntity, TResult>),
            AzureTableDriverDynamic.ConditionForLockingDelegateAsync<TEntity, TResult> shouldLock =
                        default(AzureTableDriverDynamic.ConditionForLockingDelegateAsync<TEntity, TResult>),
            StorageTables.Driver.AzureStorageDriver.RetryDelegateAsync<Task<TResult>> onTimeout =
                default(StorageTables.Driver.AzureStorageDriver.RetryDelegateAsync<Task<TResult>>),
            Func<TEntity, TEntity> mutateUponLock = default(Func<TEntity, TEntity>))
            where TEntity : IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .LockedUpdateAsync(entityRef.id,
                        lockedPropertyExpression,
                    onLockAquired,
                    onNotFound: onNotFound,
                    onLockRejected: onLockRejected,
                    onAlreadyLocked: onAlreadyLocked,
                    shouldLock: shouldLock,
                    onTimeout: onTimeout,
                    mutateUponLock: mutateUponLock);
        }

        public static Task<TResult> StorageCreateAsync<TEntity, TResult>(this TEntity entity,
            Func<EastFive.Persistence.Azure.StorageTables.IAzureStorageTableEntity<TEntity>, TResult> onCreated,
            Func<TResult> onAlreadyExists,
            IHandleFailedModifications<TResult>[] onModificationFailures =
                default(IHandleFailedModifications<TResult>[]))
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .CreateAsync(entity,
                    onCreated,
                    onAlreadyExists,
                    onModificationFailures: onModificationFailures);
        }

        public static Task<TResult> StorageReplaceAsync<TEntity, TResult>(this TEntity entity,
            Func<TResult> onSuccess,
            Func<StorageTables.Driver.ExtendedErrorInformationCodes, string, TResult> onFailure = null,
            StorageTables.Driver.AzureStorageDriver.RetryDelegate onTimeout = null)
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .ReplaceAsync(entity,
                    onSuccess, 
                    onFailure: onFailure,
                    onTimeout:onTimeout);
        }
        

        #region Transactions

        public static async Task<ITransactionResult<TResult>> CheckAsync<T, TResult>(this IRef<T> value,
            Func<TResult> onNotFound)
            where T : struct, IReferenceable
        {
            if (value.IsDefaultOrNull())
                return onNotFound().TransactionResultFailure();

            return await value.StorageGetAsync(
                valueValue =>
                {
                    Func<Task> rollback = () => 1.AsTask();
                    return rollback.TransactionResultSuccess<TResult>();
                },
                () => onNotFound().TransactionResultFailure());
        }

        public static async Task<ITransactionResult<TResult>> TransactionUpdateLinkN1Async<T, TLink, TResult>(this T value,
            Func<T, IRefOptional<TLink>> linkedOutOptional,
            Expression<Func<TLink, IRefs<T>>> linkedBack,
            Func<TResult> onNotFound)
            where T : struct, IReferenceable where TLink : struct, IReferenceable
        {
            var refOptional = linkedOutOptional(value);
            if(!refOptional.HasValue)
            {
                Func<Task> rollbackValues =
                        () => true.AsTask();
                return rollbackValues.TransactionResultSuccess<TResult>();
            }
            var linkedOut = refOptional.Ref;
            return await value.TransactionUpdateLinkN1Async(
                (res) => linkedOut,
                linkedBack,
                onNotFound);
        }

        public static Task<ITransactionResult<TResult>> TransactionUpdateLinkN1Async<T, TLink, TResult>(this T value,
            Func<T, IRef<TLink>> linkedOut,
            Expression<Func<TLink, IRefs<T>>> linkedBack,
            Func<TResult> onNotFound)
            where T : struct, IReferenceable where TLink : struct, IReferenceable
        {
            var driver = AzureTableDriverDynamic
                   .FromSettings();

            var linkRef = linkedOut(value);
            if (linkRef.IsDefaultOrNull())
                return onNotFound().TransactionResultFailure().AsTask();

            var xmemberExpr = (linkedBack.Body as MemberExpression);
            if (xmemberExpr.IsDefaultOrNull())
                throw new Exception($"`{linkedBack.Body}` is not a member expression");
            var memberInfo = xmemberExpr.Member;
            return linkRef.StorageUpdateAsync(
                async (linkedValue, updateAsync) =>
                {
                    var linkRefsOld = (IRefs<T>)memberInfo.GetValue(linkedValue);

                    if (linkRefsOld.ids.Contains(value.id))
                    {
                        Func<Task> rollback = () => 1.AsTask();
                        return rollback.TransactionResultSuccess<TResult>();
                    }

                    var linkIdsNew = linkRefsOld.ids.Append(value.id).ToArray();
                    var linkRefsNew = new Refs<T>(linkIdsNew);
                    memberInfo.SetValue(ref linkedValue, linkRefsNew);
                    await updateAsync(linkedValue);

                    Func<Task> rollbackValues = 
                        () => linkRef.StorageUpdateAsync(
                            async (linkedValueRollback, updateAsyncRollback) =>
                            {
                                var linkRefsOldRollback = (IRefs<T>)memberInfo.GetValue(linkedValueRollback);
                                if (linkRefsOld.ids.Contains(value.id))
                                    return false;

                                var linkIdsNewRollback = linkRefsOldRollback.ids.Where(id => id != value.id).ToArray();
                                var linkRefsNewRollback = new Refs<T>(linkIdsNewRollback);
                                memberInfo.SetValue(ref linkedValueRollback, linkRefsNewRollback);
                                await updateAsyncRollback(linkedValueRollback);
                                return true;
                            },
                            () => false);
                    return rollbackValues.TransactionResultSuccess<TResult>();
                },
                () => onNotFound().TransactionResultFailure());
            
        }

        public static async Task<ITransactionResult<TResult>> StorageCreateTransactionAsync<TEntity, TResult>(this TEntity entity,
            Func<TResult> onAlreadyExists)
        {
            var driver = AzureTableDriverDynamic
                .FromSettings();
            return await driver
                .CreateAsync(entity,
                    (tableEntity) =>
                    {
                        Func<Task> rollback = (() =>
                        {
                            return driver.DeleteAsync<TEntity, bool>(tableEntity.RowKey, tableEntity.PartitionKey,
                                () => true,
                                () => false);
                        });
                        return rollback.TransactionResultSuccess<TResult>();
                    },
                    () => onAlreadyExists().TransactionResultFailure());
        }

        #endregion
    }
}

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

namespace EastFive.Azure.Persistence.AzureStorageTables
{
    public static class StorageExtensions
    {

        public static Task<TResult> StorageGetAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
            Func<TEntity, TResult> onFound,
            Func<TResult> onDoesNotExists = default(Func<TResult>))
            where TEntity : struct, IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .FindByIdAsync(entityRef.id,
                    onFound,
                    onDoesNotExists);
        }

        public static Task<TResult> StorageGetAsync<TEntity, TResult>(this Guid resourceId,
            Func<TEntity, TResult> onFound,
            Func<TResult> onDoesNotExists = default(Func<TResult>))
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .FindByIdAsync<TEntity, TResult>(resourceId,
                    onFound,
                    onDoesNotExists);
        }

        public static IRef<TEntity> IRefStorage<TEntity>(this Guid entityId)
            where TEntity : struct
        {
            return new Ref<TEntity>(entityId);
        }

        public static IRefObj<TEntity> IRefObjStorage<TEntity>(this Guid entityId)
            where TEntity : class
        {
            return new RefObj<TEntity>(entityId);
        }

        public static IEnumerableAsync<TEntity> StorageQuery<TEntity>(
            this Expression<Func<TEntity, bool>> query)
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .FindAll(query);
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
            Func<bool, TEntity, Func<TEntity, Task>, Task<TResult>> onCreated)
            where TEntity : struct, IReferenceable
        {
            var documentId = entityRef.id;
            return AzureTableDriverDynamic
                .FromSettings()
                .UpdateOrCreatesAsync<TEntity, TResult>(documentId,
                    onCreated);
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
            where TEntity : struct
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

        public static Task<TResult> StorageLockedUpdateAsync<TEntity, TResult>(this IRefObj<TEntity> entityRef,
                Expression<Func<TEntity, DateTime?>> lockedPropertyExpression,
            AzureTableDriverDynamic.WhileLockedDelegateAsync<TEntity, TResult> onLockAquired,
            Func<Task<TResult>> onNotFound,
            Func<TResult> onLockRejected = default(Func<TResult>),
            AzureTableDriverDynamic.ContinueAquiringLockDelegateAsync<TEntity, TResult> onAlreadyLocked =
                        default(AzureTableDriverDynamic.ContinueAquiringLockDelegateAsync<TEntity, TResult>),
            AzureTableDriverDynamic.ConditionForLockingDelegateAsync<TEntity, TResult> shouldLock =
                        default(AzureTableDriverDynamic.ConditionForLockingDelegateAsync<TEntity, TResult>),
            StorageTables.Driver.AzureStorageDriver.RetryDelegateAsync<Task<TResult>> onTimeout =
                default(StorageTables.Driver.AzureStorageDriver.RetryDelegateAsync<Task<TResult>>),
            Func<TEntity, TEntity> mutateUponLock = default(Func<TEntity, TEntity>))
            where TEntity : class
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
            Func<Guid, TResult> onCreated,
            Func<TResult> onAlreadyExists)
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .CreateAsync(entity,
                    onCreated,
                    onAlreadyExists);
        }


        #region Transactions

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
            Func<T, IRefObj<TLink>> linkedOut,
            Expression<Func<TLink, IReferences>> linkedBack,
            Func<TResult> onNotFound)
            where T : class, IReferenceable where TLink : class, IReferenceable
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
                    var linkRefsOld = (IReferences)memberInfo.GetValue(linkedValue);

                    if (linkRefsOld.ids.Contains(value.id))
                    {
                        Func<Task> rollback = () => 1.AsTask();
                        return rollback.TransactionResultSuccess<TResult>();
                    }

                    var linkIdsNew = linkRefsOld.ids.Append(value.id).ToArray();
                    var linkRefsNew = new RefObjs<T>(linkIdsNew);
                    memberInfo.SetValue(ref linkedValue, linkRefsNew);
                    await updateAsync(linkedValue);

                    Func<Task> rollbackValues =
                        () => linkRef.StorageUpdateAsync(
                            async (linkedValueRollback, updateAsyncRollback) =>
                            {
                                var linkRefsOldRollback = (IRefObjs<T>)memberInfo.GetValue(linkedValueRollback);
                                if (linkRefsOld.ids.Contains(value.id))
                                    return false;

                                var linkIdsNewRollback = linkRefsOldRollback.ids.Where(id => id != value.id).ToArray();
                                var linkRefsNewRollback = new RefObjs<T>(linkIdsNewRollback);
                                memberInfo.SetValue(ref linkedValueRollback, linkRefsNewRollback);
                                await updateAsyncRollback(linkedValueRollback);
                                return true;
                            },
                            () => false);
                    return rollbackValues.TransactionResultSuccess<TResult>();
                },
                () => onNotFound().TransactionResultFailure());

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

        #endregion
    }
}

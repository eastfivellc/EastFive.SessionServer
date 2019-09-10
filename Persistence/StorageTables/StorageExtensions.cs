﻿using System;
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
using BlackBarLabs.Extensions;
using System.Reflection;

namespace EastFive.Azure.Persistence.AzureStorageTables
{
    public static class StorageExtensions
    {
        #region Row / Partition keys

        public static string StorageComputeRowKey<TEntity>(this IRef<TEntity> entityRef,
                Func<EastFive.Persistence.IComputeAzureStorageTableRowKey> onMissing = default)
            where TEntity : IReferenceable
        {
            var partitionKeyMember = typeof(TEntity)
                .GetPropertyOrFieldMembers()
                .Where(member => member.ContainsAttributeInterface<EastFive.Persistence.IComputeAzureStorageTableRowKey>())
                .Select(member =>
                    member.GetAttributesInterface<EastFive.Persistence.IComputeAzureStorageTableRowKey>()
                        .First()
                        .PairWithKey(member))
                .First(
                    (computeAzureStorageTableRowKey, next) => computeAzureStorageTableRowKey,
                    () =>
                    {
                        if (onMissing.IsDefaultOrNull())
                            throw new Exception($"{typeof(TEntity).FullName} is missing attribute implementing {typeof(EastFive.Persistence.IComputeAzureStorageTableRowKey).FullName}.");
                        return onMissing().PairWithKey(default(MemberInfo));
                    });
            return partitionKeyMember.Value.ComputeRowKey(entityRef, partitionKeyMember.Key);
        }

        public static string StorageGetRowKey<TEntity>(this TEntity entity)
        {
            var partitionKeyMember = typeof(TEntity)
                .GetPropertyOrFieldMembers()
                .Where(member => member.ContainsAttributeInterface<EastFive.Persistence.IModifyAzureStorageTableRowKey>())
                .Select(member =>
                    member.GetAttributesInterface<EastFive.Persistence.IModifyAzureStorageTableRowKey>()
                        .First()
                        .PairWithKey(member))
                .First();
            return partitionKeyMember.Value.GenerateRowKey(entity, partitionKeyMember.Key);
        }

        public static TEntity StorageParseRowKey<TEntity>(this TEntity entity, string rowKey)
        {
            var partitionKeyMember = typeof(TEntity)
                .GetPropertyOrFieldMembers()
                .Where(member => member.ContainsAttributeInterface<EastFive.Persistence.IModifyAzureStorageTableRowKey>())
                .Select(member =>
                    member.GetAttributesInterface<EastFive.Persistence.IModifyAzureStorageTableRowKey>()
                        .First()
                        .PairWithKey(member))
                .First();
            return partitionKeyMember.Value.ParseRowKey(entity, rowKey, partitionKeyMember.Key);
        }

        public static string StorageComputePartitionKey<TEntity>(this IRef<TEntity> entityRef,
                Func<EastFive.Persistence.IComputeAzureStorageTablePartitionKey> onMissing = default)
            where TEntity : IReferenceable
        {
            var rowKey = entityRef.StorageComputeRowKey();
            return entityRef.StorageComputePartitionKey(rowKey, onMissing: onMissing);
        }

        public static string StorageComputePartitionKey<TEntity>(this IRef<TEntity> entityRef, string rowKey,
                Func<EastFive.Persistence.IComputeAzureStorageTablePartitionKey> onMissing = default)
            where TEntity : IReferenceable
        {
            var partitionKeyMember = typeof(TEntity)
                .GetPropertyOrFieldMembers()
                .Where(member => member.ContainsAttributeInterface<EastFive.Persistence.IComputeAzureStorageTablePartitionKey>())
                .Select(member =>
                    member.GetAttributesInterface<EastFive.Persistence.IComputeAzureStorageTablePartitionKey>()
                        .First()
                        .PairWithKey(member))
                .First(
                    (computeAzureStorageTableParitionKey, next) => computeAzureStorageTableParitionKey,
                    () =>
                    {
                        if (onMissing.IsDefaultOrNull())
                            throw new Exception($"{typeof(TEntity).FullName} is missing attribute implementing {typeof(EastFive.Persistence.IComputeAzureStorageTableRowKey).FullName}.");
                        return onMissing().PairWithKey(default(MemberInfo));
                    });
            return partitionKeyMember.Value.ComputePartitionKey(entityRef, rowKey, partitionKeyMember.Key);
        }

        public static string StorageGetPartitionKey<TEntity>(this TEntity entity)
        {
            var rowKey = entity.StorageGetRowKey();
            var partitionKeyMember = typeof(TEntity)
                .GetPropertyOrFieldMembers()
                .Where(member => member.ContainsAttributeInterface<EastFive.Persistence.IModifyAzureStorageTablePartitionKey>())
                .Select(member =>
                    member.GetAttributesInterface<EastFive.Persistence.IModifyAzureStorageTablePartitionKey>()
                        .First()
                        .PairWithKey(member))
                .First();
            return partitionKeyMember.Value.GeneratePartitionKey(rowKey: rowKey, entity, partitionKeyMember.Key);
        }

        public static TEntity StorageParsePartitionKey<TEntity>(this TEntity entity, string partitionKey)
        {
            var partitionKeyMember = typeof(TEntity)
                .GetPropertyOrFieldMembers()
                .Where(member => member.ContainsAttributeInterface<EastFive.Persistence.IModifyAzureStorageTablePartitionKey>())
                .Select(member =>
                    member.GetAttributesInterface<EastFive.Persistence.IModifyAzureStorageTablePartitionKey>()
                        .First()
                        .PairWithKey(member))
                .First();
            return partitionKeyMember.Value.ParsePartitionKey(entity, partitionKey, partitionKeyMember.Key);
        }

        public static IEnumerable<string> StorageGetPartitionKeys(this Type type, int skip, int top)
        {
            var partitionKeyMember = type
                .GetPropertyOrFieldMembers()
                .Where(member => member.ContainsAttributeInterface<EastFive.Persistence.IModifyAzureStorageTablePartitionKey>())
                .Select(member =>
                    member.GetAttributesInterface<EastFive.Persistence.IModifyAzureStorageTablePartitionKey>()
                        .First()
                        .PairWithKey(member))
                .First();
            return partitionKeyMember.Value.GeneratePartitionKeys(type, skip: skip, top: top);
        }

        #endregion

        #region Metadata

        public static Task<StorageTables.TableInformation> StorageTableInformationAsync(this Type entityType,
            CloudTable table = default,
            string tableName = default,
            int numberOfTimesToRetry = AzureTableDriverDynamic.DefaultNumberOfTimesToRetry)
        {
            var driver = AzureTableDriverDynamic.FromSettings();
            var tableInformationTaskObj = typeof(AzureTableDriverDynamic)
                .GetMethod("TableInformationAsync", BindingFlags.Instance | BindingFlags.Public)
                .MakeGenericMethod(entityType.AsArray())
                .Invoke(driver, new object[] { table, tableName, numberOfTimesToRetry });

            var tableInformationTask = tableInformationTaskObj as Task<StorageTables.TableInformation>;
            return tableInformationTask;
        }

        #endregion

        #region QUERY / GET

        public static Task<TResult> StorageGetAsync<TEntity, TResult>(this Guid resourceId,
            Func<TEntity, TResult> onFound,
            Func<TResult> onDoesNotExists = default(Func<TResult>),
            Func<string> getPartitionKey = default(Func<string>))
            where TEntity : IReferenceable
        {
            return resourceId
                .AsRef<TEntity>()
                .StorageGetAsync(
                    onFound,
                    onDoesNotExists);
        }

        public static async Task<TResult> StorageGetAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
            Func<TEntity, TResult> onFound,
            Func<TResult> onDoesNotExists = default)
            where TEntity : IReferenceable
        {
            if (entityRef.IsDefaultOrNull())
                return onDoesNotExists();
            var rowKey = entityRef.StorageComputeRowKey();
            var partitionKey = entityRef.StorageComputePartitionKey();
            return await AzureTableDriverDynamic
                .FromSettings()
                .FindByIdAsync(rowKey, partitionKey,
                    onFound,
                    onDoesNotExists);
        }

        [Obsolete]
        public static async Task<TResult> StorageGetAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
            string partitionKey,
            Func<TEntity, TResult> onFound,
            Func<TResult> onDoesNotExists = default)
            where TEntity : IReferenceable
        {
            if (entityRef.IsDefaultOrNull())
                return onDoesNotExists();
            var rowKey = entityRef.StorageComputeRowKey();
            return await AzureTableDriverDynamic
                .FromSettings()
                .FindByIdAsync(rowKey, partitionKey,
                    onFound,
                    onDoesNotExists);
        }

        public static IEnumerableAsync<TEntity> StorageGetBy<TRefEntity, TEntity>(this IRef<TRefEntity> entityRef,
                Expression<Func<TEntity, IRef<TRefEntity>>> by)
            where TEntity : IReferenceable
            where TRefEntity : IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .FindBy(entityRef, by);
        }

        public static IEnumerableAsync<TEntity> StorageGetBy<TRefEntity, TEntity>(this IRef<TRefEntity> entityRef,
                Expression<Func<TEntity, IRefOptional<TRefEntity>>> by)
            where TEntity : IReferenceable
            where TRefEntity : IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .FindBy(entityRef, by);
        }

        public static IEnumerableAsync<TEntity> StorageGetBy<TRefEntity, TEntity>(this IRef<TRefEntity> entityRef,
                Expression<Func<TEntity, IRef<IReferenceable>>> by)
            where TEntity : IReferenceable
            where TRefEntity : IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .FindBy(entityRef, by);
        }

        public static IEnumerableAsync<TEntity> StorageGetBy<TRefEntity, TEntity>(this IRef<TRefEntity> entityRef,
                Expression<Func<TEntity, IRefs<TRefEntity>>> by)
            where TEntity : IReferenceable
            where TRefEntity : IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .FindBy(entityRef, by);
        }

        [Obsolete("Use IRef in place of IRefObj")]
        public static Task<TResult> StorageGetAsync<TEntity, TResult>(this IRefObj<TEntity> entityRefObj,
            Func<TEntity, TResult> onFound,
            Func<TResult> onDoesNotExists = default,
            Func<string> getPartitionKey = default)
            where TEntity : class, IReferenceable
        {
            if (getPartitionKey.IsDefaultOrNull())
                getPartitionKey = () => entityRefObj.id.AsRowKey().GeneratePartitionKey();

            return AzureTableDriverDynamic
                .FromSettings()
                .FindByIdAsync(entityRefObj.id.AsRowKey(), getPartitionKey(),
                    onFound,
                    onDoesNotExists);
        }

        public static Task<TResult> StorageGetAsync<TEntity, TResult>(this IRefOptional<TEntity> entityRefMaybe,
            Func<TEntity, TResult> onFound,
            Func<TResult> onDoesNotExists = default)
            where TEntity : struct, IReferenceable
        {
            if (!entityRefMaybe.HasValueNotNull())
                return onDoesNotExists().AsTask();

            var entityRef = entityRefMaybe.Ref;
            return StorageGetAsync(entityRef,
                onFound,
                onDoesNotExists);
        }

        public static IEnumerableAsync<TEntity> StorageGet<TEntity>(this IRefs<TEntity> entityRefs)
            where TEntity : IReferenceable
        {
            var keys = entityRefs.refs
                .Select(r => r.StorageComputeRowKey()
                    .PairWithValue(r.StorageComputePartitionKey()))
                .ToArray();
            return AzureTableDriverDynamic
                .FromSettings()
                .FindByIdsAsync<TEntity>(keys);
        }

        public static IEnumerableAsync<TEntity> StorageQuery<TEntity>(
            this Expression<Func<TEntity, bool>> query)
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .FindAll(query);
        }

        public static IEnumerableAsync<TEntity> StorageGetPartition<TEntity>(this string partition)
            where TEntity : IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .FindEntityBypartition<TEntity>(partition);
        }

        #endregion

        #region Dictionary

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

        [Obsolete("Use string row/partition keys")]
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
            where TEntity : IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .DeleteAll(query);
        }

        #endregion

        #region Create

        public static Task<TResult> StorageCreateAsync<TEntity, TResult>(this TEntity entity,
            Func<EastFive.Persistence.Azure.StorageTables.IAzureStorageTableEntity<TEntity>, TResult> onCreated,
            Func<TResult> onAlreadyExists = default,
            params IHandleFailedModifications<TResult>[] onModificationFailures)
            where TEntity : IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .CreateAsync(entity,
                    onCreated,
                    onAlreadyExists,
                    onModificationFailures: onModificationFailures);
        }

        #endregion

        #region CreateOrUpdate

        [Obsolete("Set ID is no longer necessary with property row / partition attributes")]
        public static Task<TResult> StorageCreateOrUpdateAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
            Func<TEntity, TEntity> setId,
            Func<bool, TEntity, Func<TEntity, Task>, Task<TResult>> onCreated)
            where TEntity : struct, IReferenceable
        {
            var rowKey = entityRef.StorageComputeRowKey();
            var partitionKey = entityRef.StorageComputePartitionKey();
            return AzureTableDriverDynamic
                .FromSettings()
                .UpdateOrCreateAsync<TEntity, TResult>(rowKey, partitionKey,
                    setId,
                    onCreated,
                    default(AzureStorageDriver.RetryDelegateAsync<Task<TResult>>));
        }

        public static Task<TResult> StorageCreateOrUpdateAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
            Func<bool, TEntity, Func<TEntity, Task>, Task<TResult>> onCreated)
            where TEntity : struct, IReferenceable
        {
            var rowKey = entityRef.StorageComputeRowKey();
            var partitionKey = entityRef.StorageComputePartitionKey();
            return AzureTableDriverDynamic
                .FromSettings()
                .UpdateOrCreateAsync<TEntity, TResult>(rowKey, partitionKey,
                    onCreated,
                    default);
        }
        [Obsolete]
        public static Task<TResult> StorageCreateOrUpdateAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
            string partitionKey,
            Func<bool, TEntity, Func<TEntity, Task>, Task<TResult>> onCreated)
            where TEntity : struct, IReferenceable
        {
            var rowKey = entityRef.StorageComputeRowKey();
            return AzureTableDriverDynamic
                .FromSettings()
                .UpdateOrCreateAsync<TEntity, TResult>(rowKey, partitionKey,
                    onCreated,
                    default(AzureStorageDriver.RetryDelegateAsync<Task<TResult>>));
        }

        public static IEnumerableAsync<TResult> StorageCreateOrUpdateBatch<TEntity, TResult>(this IEnumerable<TEntity> entities,
            Func<TEntity, Microsoft.WindowsAzure.Storage.Table.TableResult, TResult> perItemCallback,
            string tableName = default(string),
            Azure.StorageTables.Driver.AzureStorageDriver.RetryDelegate onTimeout = default,
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
                    diagnostics: diagnostics);
        }

        public static IEnumerableAsync<TResult> StorageCreateOrUpdateBatch<TEntity, TResult>(this IEnumerableAsync<TEntity> entities,
            Func<TEntity, Microsoft.WindowsAzure.Storage.Table.TableResult, TResult> perItemCallback,
            string tableName = default(string),
            Azure.StorageTables.Driver.AzureStorageDriver.RetryDelegate onTimeout = default,
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
                    diagnostics: diagnostics);
        }

        #endregion

        #region Update

        public static Task<TResult> StorageReplaceAsync<TEntity, TResult>(this TEntity entity,
            Func<TResult> onSuccess,
            Func<Azure.StorageTables.Driver.ExtendedErrorInformationCodes, string, TResult> onFailure = null,
            AzureStorageDriver.RetryDelegate onTimeout = null)
            where TEntity : IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .ReplaceAsync(entity,
                    onSuccess,
                    onFailure: onFailure,
                    onTimeout: onTimeout);
        }

        public static Task<TResult> StorageUpdateAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
            Func<TEntity, Func<TEntity, Task>, Task<TResult>> onUpdate,
            Func<TResult> onNotFound = default,
            Azure.StorageTables.Driver.AzureStorageDriver.RetryDelegateAsync<Task<TResult>> onTimeoutAsync =
                default(Azure.StorageTables.Driver.AzureStorageDriver.RetryDelegateAsync<Task<TResult>>))
            where TEntity : IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .UpdateAsync(
                        entityRef.StorageComputeRowKey(),
                        entityRef.StorageComputePartitionKey(),
                    onUpdate,
                    onNotFound: onNotFound,
                    onTimeoutAsync: onTimeoutAsync);
        }

        public static Task<TResult> StorageUpdateAsyncAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
            Func<TEntity, Func<TEntity, Task>, Task<TResult>> onUpdate,
            Func<Task<TResult>> onNotFound = default,
            Azure.StorageTables.Driver.AzureStorageDriver.RetryDelegateAsync<Task<TResult>> onTimeoutAsync =
                default(Azure.StorageTables.Driver.AzureStorageDriver.RetryDelegateAsync<Task<TResult>>))
            where TEntity : struct, IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .UpdateAsyncAsync(entityRef.StorageComputeRowKey(), entityRef.StorageComputePartitionKey(),
                    onUpdate,
                    onNotFound: onNotFound,
                    onTimeoutAsync: onTimeoutAsync);
        }

        [Obsolete("Replace IRefObj with IRef")]
        public static Task<TResult> StorageUpdateAsync<TEntity, TResult>(this IRefObj<TEntity> entityRef,
            Func<TEntity, Func<TEntity, Task>, Task<TResult>> onUpdate,
            Func<TResult> onNotFound = default(Func<TResult>),
            Azure.StorageTables.Driver.AzureStorageDriver.RetryDelegateAsync<Task<TResult>> onTimeoutAsync = default)
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

        #endregion

        #region Delete

        public static Task<TResult> StorageDeleteAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
            Func<TResult> onSuccess,
            Func<TResult> onNotFound = default)
            where TEntity : struct, IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .DeleteAsync<TEntity, TResult>(
                        entityRef.StorageComputeRowKey(),
                        entityRef.StorageComputePartitionKey(),
                    onSuccess,
                    onNotFound);
        }

        public static Task<TResult> StorageDeleteAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
                string partitionKey,
            Func<TResult> onSuccess,
            Func<TResult> onNotFound = default)
            where TEntity : struct, IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .DeleteAsync<TEntity, TResult>(
                        entityRef.StorageComputeRowKey(),
                        partitionKey,
                    onSuccess,
                    onNotFound);
        }

        [Obsolete]
        public static Task<TResult> StorageDeleteAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
            Func<TEntity, Func<Task>, Task<TResult>> onFound,
            Func<TResult> onNotFound = default)
            where TEntity : struct, IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .DeleteAsync(
                        entityRef.StorageComputeRowKey(),
                        entityRef.StorageComputePartitionKey(),
                    onFound,
                    onNotFound);
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

        #endregion

        #region Locks

        public static Task<TResult> StorageLockedUpdateAsync<TEntity, TResult>(this IRef<TEntity> entityRef,
                Expression<Func<TEntity, DateTime?>> lockedPropertyExpression,
            AzureTableDriverDynamic.WhileLockedDelegateAsync<TEntity, TResult> onLockAquired,
            Func<TResult> onNotFound,
            Func<TResult> onLockRejected = default(Func<TResult>),
            AzureTableDriverDynamic.ContinueAquiringLockDelegateAsync<TEntity, TResult> onAlreadyLocked =
                        default(AzureTableDriverDynamic.ContinueAquiringLockDelegateAsync<TEntity, TResult>),
            AzureTableDriverDynamic.ConditionForLockingDelegateAsync<TEntity, TResult> shouldLock =
                        default(AzureTableDriverDynamic.ConditionForLockingDelegateAsync<TEntity, TResult>),
            Azure.StorageTables.Driver.AzureStorageDriver.RetryDelegateAsync<Task<TResult>> onTimeout = default,
            Func<TEntity, TEntity> mutateUponLock = default(Func<TEntity, TEntity>))
            where TEntity : IReferenceable
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .LockedUpdateAsync(
                        entityRef.StorageComputeRowKey(),
                        entityRef.StorageComputePartitionKey(),
                        lockedPropertyExpression,
                    onLockAquired,
                    onNotFound: onNotFound,
                    onLockRejected: onLockRejected,
                    onAlreadyLocked: onAlreadyLocked,
                    shouldLock: shouldLock,
                    onTimeout: onTimeout,
                    mutateUponLock: mutateUponLock);
        }

        #endregion

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
            where TEntity : IReferenceable
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

        #region BLOB

        public static Task<TResult> BlobCreateAsync<TResult>(this byte[] content, string containerName,
            Func<Guid, TResult> onSuccess,
            Func<Azure.StorageTables.Driver.ExtendedErrorInformationCodes, string, TResult> onFailure = default,
            string contentType = default,
            Azure.StorageTables.Driver.AzureStorageDriver.RetryDelegate onTimeout = null)
        {
            var blobId = Guid.NewGuid();
            return content.BlobCreateAsync(blobId, containerName,
                () => onSuccess(blobId),
                () => throw new Exception("Guid not unique."),
                onFailure: onFailure,
                contentType: contentType,
                onTimeout: onTimeout);
        }

        public static Task<TResult> BlobCreateAsync<TResult>(this byte[] content, Guid blobId, string containerName,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyExists,
            Func<Azure.StorageTables.Driver.ExtendedErrorInformationCodes, string, TResult> onFailure = default,
            string contentType = default,
            Azure.StorageTables.Driver.AzureStorageDriver.RetryDelegate onTimeout = null)
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .BlobCreateAsync(content, blobId, containerName,
                    onSuccess,
                    onAlreadyExists: onAlreadyExists,
                    onFailure: onFailure,
                    contentType: contentType,
                    onTimeout: onTimeout);
        }

        public static Task<TResult> BlobLoadBytesAsync<TResult>(this Guid blobId, string containerName,
            Func<byte [], string, TResult> onSuccess,
            Func<TResult> onNotFound = default,
            Func<Azure.StorageTables.Driver.ExtendedErrorInformationCodes, string, TResult> onFailure = default,
            Azure.StorageTables.Driver.AzureStorageDriver.RetryDelegate onTimeout = null)
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .BlobLoadBytesAsync(blobId, containerName,
                    onSuccess,
                    onNotFound,
                    onFailure: onFailure,
                    onTimeout: onTimeout);
        }

        public static Task<TResult> BlobLoadStreamAsync<TResult>(this Guid blobId, string containerName,
            Func<System.IO.Stream, string, TResult> onSuccess,
            Func<TResult> onNotFound = default,
            Func<Azure.StorageTables.Driver.ExtendedErrorInformationCodes, string, TResult> onFailure = default,
            Azure.StorageTables.Driver.AzureStorageDriver.RetryDelegate onTimeout = null)
        {
            return AzureTableDriverDynamic
                .FromSettings()
                .BlobLoadStreamAsync(blobId, containerName,
                    onSuccess,
                    onNotFound,
                    onFailure: onFailure,
                    onTimeout: onTimeout);
        }

        #endregion
    }
}

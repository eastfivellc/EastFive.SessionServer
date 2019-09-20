using BlackBarLabs;
using BlackBarLabs.Extensions;
using BlackBarLabs.Persistence.Azure.StorageTables;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Linq.Expressions;
using EastFive.Persistence.Azure.StorageTables.Driver;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Persistence.Azure.StorageTables
{
    public class StorageLookupAttribute : Attribute,
        IModifyAzureStorageTableSave, IProvideFindBy
    {
        public string LookupTableName { get; set; }

        public string Scope { get; set; }

        public Type RowKeyAttribute { get; set; }

        public Type PartitionAttribute { get; set; }

        public interface IScope
        {
            string GetHashValue(MemberInfo memberInfo);
        }

        public class ScopingAttribute : Attribute, IScope
        {
            public string Scope { get; set; }

            public ScopingAttribute(string scope)
            {
                this.Scope = scope;
            }

            public string GetHashValue(MemberInfo memberInfo)
            {
                return this.Scope;
            }
        }

        public virtual string ComputeLookupRowKey(object memberValue, MemberInfo memberInfo)
        {
            if (!this.RowKeyAttribute.IsDefaultOrNull())
            {
                var rowKeyComputer = (IComputeAzureStorageTableRowKey)Activator.CreateInstance(RowKeyAttribute);
                return rowKeyComputer.ComputeRowKey(memberValue, memberInfo);
            }
            return memberInfo.StorageComputeRowKey(memberValue,
                () => new RowKeyAttribute());
        }

        public virtual string ComputeLookupPartitionKey(object memberValue, MemberInfo memberInfo, string rowKey)
        {
            var scopes = memberInfo.DeclaringType
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(member => member.ContainsAttributeInterface<IScope>())
                .Select(
                    member =>
                    {
                        return member.GetAttributesInterface<IScope>().First()
                            .GetHashValue(member);
                    })
                .Join(string.Empty);
            if (scopes.HasBlackSpace())
                return scopes;

            if (!this.PartitionAttribute.IsDefaultOrNull())
            {
                var partitionKeyComputer = (IComputeAzureStorageTablePartitionKey)Activator.CreateInstance(RowKeyAttribute);
                return partitionKeyComputer.ComputePartitionKey(memberValue, memberInfo, rowKey);
            }

            return memberInfo.StorageComputePartitionKey(memberValue, rowKey,
                () => new StandardParititionKeyAttribute());
        }

        //public virtual IRefAst< ComputeLookupRef(object memberValue, MemberInfo memberInfo, string rowKey)
        //{
        //    // TODO: Check for Scopes
        //    if (!this.PartitionAttribute.IsDefaultOrNull())
        //    {
        //        var partitionKeyComputer = (IComputeAzureStorageTablePartitionKey)Activator.CreateInstance(RowKeyAttribute);
        //        return partitionKeyComputer.ComputePartitionKey(memberValue, memberInfo, rowKey);
        //    }
        //    return memberInfo.StorageComputePartitionKey(memberValue, rowKey,
        //        () => new StandardParititionKeyAttribute());
        //}

        private string GetLookupTableName(MemberInfo memberInfo)
        {
            if (LookupTableName.HasBlackSpace())
                return this.LookupTableName;
            return $"{memberInfo.DeclaringType.Name}{memberInfo.Name}";
        }

        public IEnumerableAsync<IRefAst> GetKeys(object memberValue,
                MemberInfo memberInfo, Driver.AzureTableDriverDynamic repository)
        {
            var tableName = GetLookupTableName(memberInfo);
            var lookupRowKey = ComputeLookupRowKey(memberValue, memberInfo);
            var lookupPartitionKey = ComputeLookupPartitionKey(memberValue, memberInfo, lookupRowKey);
            return repository
                .FindByIdAsync<StorageLookupTable, IEnumerableAsync<IRefAst>>(lookupRowKey, lookupPartitionKey,
                    (dictEntity) =>
                    {
                        var rowAndParitionKeys = dictEntity.rowAndPartitionKeys
                            .NullToEmpty()
                            .Select(rowParitionKeyKvp => rowParitionKeyKvp.Key.AsAstRef(rowParitionKeyKvp.Value))
                            .AsAsync();
                        return rowAndParitionKeys;
                    },
                    () => EnumerableAsync.Empty<IRefAst>(),
                    tableName: tableName)
                .FoldTask();
        }

        [StorageTable]
        public struct StorageLookupTable
        {

            [RowKey]
            public string rowKey;

            [ParititionKey]
            public string partitionKey;

            [Storage]
            public KeyValuePair<string, string>[] rowAndPartitionKeys;
        }

        protected virtual IEnumerable<string> GetRowKeys<TEntity>(MemberInfo memberInfo, TEntity value)
        {
            var rowKeyValue = memberInfo.GetValue(value);
            var propertyValueType = memberInfo.GetMemberType();
            if (typeof(Guid).IsAssignableFrom(propertyValueType))
            {
                var guidValue = (Guid)rowKeyValue;
                return guidValue.AsRowKey().AsEnumerable();
            }
            if (typeof(IReferenceable).IsAssignableFrom(propertyValueType))
            {
                var refValue = (IReferenceable)rowKeyValue;
                if(refValue.IsDefaultOrNull())
                    return new string[] { };
                return refValue.id.AsRowKey().AsEnumerable();
            }
            if (typeof(string).IsAssignableFrom(propertyValueType))
            {
                var stringValue = (string)rowKeyValue;
                return stringValue.AsEnumerable();
            }
            if (typeof(IReferenceableOptional).IsAssignableFrom(propertyValueType))
            {
                var referenceableOptional = (IReferenceableOptional)rowKeyValue;
                if (referenceableOptional.IsDefaultOrNull())
                    return new string[] { };
                if (!referenceableOptional.HasValue)
                    return new string[] { };
                return referenceableOptional.id.Value.AsRowKey().AsEnumerable();
            }
            if (typeof(IReferences).IsAssignableFrom(propertyValueType))
            {
                var references = (IReferences)rowKeyValue;
                if (references.IsDefaultOrNull())
                    return new string[] { };
                return references.ids.Select(id => id.AsRowKey());
            }
            var exMsg = $"{this.GetType().Name} is not implemented for type `{propertyValueType.FullName}`. " +
                $"Please override GetRowKeys on `{this.GetType().FullName}`.";
            throw new NotImplementedException(exMsg);
        }

        protected virtual string GetPartitionKey<TEntity>(string rowKey, TEntity value, MemberInfo memberInfo)
        {
            var partitionKeyAttributeType = this.PartitionAttribute.IsDefaultOrNull() ?
                  typeof(StandardParititionKeyAttribute)
                  :
                  this.PartitionAttribute;
            if (!partitionKeyAttributeType.IsSubClassOfGeneric(typeof(IModifyAzureStorageTablePartitionKey)))
                throw new Exception($"{memberInfo.DeclaringType.FullName}..{memberInfo.Name} defines partition type as {partitionKeyAttributeType.FullName} which does not implement {typeof(IModifyAzureStorageTablePartitionKey).FullName}.");
            var partitionKeyAttribute = Activator.CreateInstance(partitionKeyAttributeType) as IModifyAzureStorageTablePartitionKey;
            var partitionKey = partitionKeyAttribute.GeneratePartitionKey(rowKey, value, memberInfo);
            return partitionKey;
        }

        public virtual async Task<TResult> ExecuteAsync<TEntity, TResult>(MemberInfo memberInfo,
                string rowKeyRef, string partitionKeyRef,
                TEntity value, IDictionary<string, EntityProperty> dictionary,
                AzureTableDriverDynamic repository,
                Func<IEnumerable<KeyValuePair<string, string>>, IEnumerable<KeyValuePair<string, string>>> mutateCollection,
            Func<Func<Task>, TResult> onSuccessWithRollback,
            Func<TResult> onFailure)
        {
            var rowKeys = GetRowKeys(memberInfo, value);
            var rollbacks = await rowKeys
                .Select(
                    rowKey =>
                    {
                        var partitionKey = GetPartitionKey(rowKey, value, memberInfo);
                        return MutateLookupTable(rowKey, partitionKey,
                            memberInfo, repository, mutateCollection);
                    })
                .WhenAllAsync();
            Func<Task> allRollbacks =
                () =>
                {
                    var tasks = rollbacks.Select(rb => rb());
                    return Task.WhenAll(tasks);
                };
            return onSuccessWithRollback(allRollbacks);
        }

        public async Task<Func<Task>> MutateLookupTable(string rowKey, string partitionKey,
            MemberInfo memberInfo, AzureTableDriverDynamic repository,
            Func<IEnumerable<KeyValuePair<string, string>>, IEnumerable<KeyValuePair<string, string>>> mutateCollection)
        {
            var tableName = GetLookupTableName(memberInfo);
            return await repository.UpdateOrCreateAsync<StorageLookupTable, Func<Task>>(rowKey, partitionKey,
                async (created, lookup, saveAsync) =>
                {
                    //lookup.rowKey = rowKey;
                    //lookup.partitionKey = partitionKey;
                    var rollbackRowAndPartitionKeys = lookup.rowAndPartitionKeys; // store for rollback
                    lookup.rowAndPartitionKeys = mutateCollection(rollbackRowAndPartitionKeys)
                        .Distinct(rpKey => rpKey.Key)
                        .ToArray();
                    await saveAsync(lookup);
                    Func<Task<bool>> rollback =
                        async () =>
                        {
                            if (created)
                            {
                                return await repository.DeleteAsync<StorageLookupTable, bool>(rowKey, partitionKey,
                                    () => true,
                                    () => false,
                                    tableName: tableName);
                            }
                            var table = repository.TableClient.GetTableReference(tableName);
                            return await repository.UpdateAsync<StorageLookupTable, bool>(rowKey, partitionKey,
                                async (modifiedDoc, saveRollbackAsync) =>
                                {
                                    bool Modified()
                                    {
                                        if (rollbackRowAndPartitionKeys.Length != modifiedDoc.rowAndPartitionKeys.Length)
                                            return true;

                                        var matchKeys = rollbackRowAndPartitionKeys.SelectKeys().AsHashSet();
                                        var matchValues = rollbackRowAndPartitionKeys.SelectKeys().AsHashSet();
                                        var allValuesAccountedFor = modifiedDoc.rowAndPartitionKeys
                                            .All(
                                                rowAndPartitionKey =>
                                                {
                                                    if (!matchKeys.Contains(rowAndPartitionKey.Key))
                                                        return false;
                                                    if (!matchValues.Contains(rowAndPartitionKey.Value))
                                                        return false;
                                                    return true;
                                                });
                                        return !allValuesAccountedFor;
                                    }
                                    if (!Modified())
                                        return true;
                                    modifiedDoc.rowAndPartitionKeys = rollbackRowAndPartitionKeys;
                                    await saveRollbackAsync(modifiedDoc);
                                    return true;
                                },
                                table: table);
                        };
                    return rollback;
                },
                tableName: tableName);
        }

        public virtual Task<TResult> ExecuteCreateAsync<TEntity, TResult>(MemberInfo memberInfo,
                string rowKeyRef, string partitionKeyRef,
                TEntity value, IDictionary<string, EntityProperty> dictionary,
                AzureTableDriverDynamic repository,
            Func<Func<Task>, TResult> onSuccessWithRollback,
            Func<TResult> onFailure)
        {
            return ExecuteAsync(memberInfo,
                rowKeyRef, partitionKeyRef,
                value, dictionary,
                repository,
                (rowAndParitionKeys) => rowAndParitionKeys.NullToEmpty().Append(rowKeyRef.PairWithValue(partitionKeyRef)),
                onSuccessWithRollback,
                onFailure);
        }

        public async Task<TResult> ExecuteUpdateAsync<TEntity, TResult>(MemberInfo memberInfo, 
                string rowKeyRef, string partitionKeyRef, 
                TEntity valueExisting, IDictionary<string, EntityProperty> dictionaryExisting,
                TEntity valueUpdated, IDictionary<string, EntityProperty> dictionaryUpdated, 
                AzureTableDriverDynamic repository, 
            Func<Func<Task>, TResult> onSuccessWithRollback, 
            Func<TResult> onFailure)
        {
            var existingRowKeys = GetRowKeys(memberInfo, valueExisting);
            var updatedRowKeys = GetRowKeys(memberInfo, valueUpdated);
            var rowKeysDeleted = existingRowKeys.Except(updatedRowKeys);
            var rowKeysAdded = updatedRowKeys.Except(existingRowKeys);
            var deletionRollbacks = rowKeysDeleted
                .Select(
                    rowKey =>
                    {
                        var partitionKey = GetPartitionKey(rowKey, valueExisting, memberInfo);
                        return MutateLookupTable(rowKey, partitionKey, memberInfo,
                            repository,
                            (rowAndParitionKeys) => rowAndParitionKeys
                                .NullToEmpty()
                                .Where(kvp => kvp.Key != rowKeyRef));
                    });
            var additionRollbacks = rowKeysAdded
                 .Select(
                     rowKey =>
                     {
                         var partitionKey = GetPartitionKey(rowKey, valueExisting, memberInfo);
                         return MutateLookupTable(rowKey, partitionKey, memberInfo,
                             repository,
                             (rowAndParitionKeys) => rowAndParitionKeys
                                .NullToEmpty()
                                .Append(rowKeyRef.PairWithValue(partitionKeyRef)));
                     });
            var allRollbacks = await additionRollbacks.Concat(deletionRollbacks).WhenAllAsync();
            Func<Task> allRollback =
                () =>
                {
                    var tasks = allRollbacks.Select(rb => rb());
                    return Task.WhenAll(tasks);
                };
            return onSuccessWithRollback(allRollback);
        }

        public Task<TResult> ExecuteDeleteAsync<TEntity, TResult>(MemberInfo memberInfo, 
                string rowKeyRef, string partitionKeyRef,
                TEntity value, IDictionary<string, EntityProperty> dictionary, 
                AzureTableDriverDynamic repository,
            Func<Func<Task>, TResult> onSuccessWithRollback, 
            Func<TResult> onFailure)
        {
            return ExecuteAsync(memberInfo,
                rowKeyRef, partitionKeyRef,
                value, dictionary,
                repository,
                (rowAndParitionKeys) => rowAndParitionKeys
                    .NullToEmpty()
                    .Where(kvp => kvp.Key != rowKeyRef),
                onSuccessWithRollback,
                onFailure);
        }


        public static IHandleFailedModifications<TResult> ModificationFailure<T, TResult>(
            Expression<Func<T, object>> property,
            Func<TResult> handlerOnFailure)
        {
            var member = property.MemberInfo(
                memberInfo => memberInfo,
                () => throw new Exception($"`{property}`: is not a member expression"));

            return new FaildModificationHandler<TResult>()
            {
                member = member,
                handler = handlerOnFailure,
            };
        }

        private class FaildModificationHandler<TResult> : IHandleFailedModifications<TResult>
        {
            internal MemberInfo member;
            internal Func<TResult> handler;

            public bool DoesMatchMember(MemberInfo[] membersWithFailures)
            {
                var doesMatchMember = membersWithFailures
                    .Where(memberWithFailure => memberWithFailure.ContainsCustomAttribute<StorageLookupAttribute>(true))
                    .Where(memberWithFailure => memberWithFailure.Name == member.Name)
                    .Any();
                return doesMatchMember;
            }

            public TResult ModificationFailure(MemberInfo[] membersWithFailures)
            {
                var failureMember = membersWithFailures
                    .Where(membersWithFailure => membersWithFailure.ContainsCustomAttribute<StorageLookupAttribute>(true))
                    .Where(memberWithFailure => memberWithFailure.Name == member.Name)
                    .First();
                return handler();
            }
        }
    }
}

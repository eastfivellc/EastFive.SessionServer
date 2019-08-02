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
    public class StorageLinkAttribute : Attribute,
        IModifyAzureStorageTableSave, IProvideFindBy
    {
        public Type ReferenceType { get; set; }

        public string ReferenceProperty { get; set; }

        public string LookupTableName { get; set; }

        public Type PartitionAttribute { get; set; }

        private string GetLookupTableName(MemberInfo memberInfo)
        {
            if (LookupTableName.HasBlackSpace())
                return this.LookupTableName;
            return $"{memberInfo.DeclaringType.Name}{memberInfo.Name}";
        }

        public IEnumerableAsync<KeyValuePair<string, string>> GetKeys<TEntity>(IRef<TEntity> value,
                Driver.AzureTableDriverDynamic repository, MemberInfo memberInfo)
            where TEntity : IReferenceable
        {
            var tableName = GetLookupTableName(memberInfo);
            var rowKey = value.StorageComputeRowKey();
            var partitionKey = value.StorageComputePartitionKey();
            return repository
                .FindByIdAsync<StorageLookupTable, IEnumerableAsync <KeyValuePair<string, string>>>(rowKey, partitionKey,
                    (dictEntity) =>
                    {
                        var rowAndParitionKeys = dictEntity.rowAndPartitionKeys.NullToEmpty()
                            .Select(rowParitionKeyKvp => rowParitionKeyKvp.AsTask())
                            .AsyncEnumerable();
                        return rowAndParitionKeys;
                    },
                    () => EnumerableAsync.Empty<KeyValuePair<string, string>>(),
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

        private string GetPartitionKey(string rowKey, object value, MemberInfo memberInfo)
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

        public virtual Task<TResult> ExecuteAsync<TEntity, TResult>(MemberInfo memberInfo,
                string rowKeyRef, string partitionKeyRef,
                TEntity value, IDictionary<string, EntityProperty> dictionary,
                AzureTableDriverDynamic repository,
            Func<Func<Task>, TResult> onSuccessWithRollback,
            Func<TResult> onFailure)
        {
            var propertyValueType = memberInfo.GetMemberType();
            async Task<TResult> GetRowKey(Func<string, Task<TResult>> callback)
            {
                var rowKeyValue = memberInfo.GetValue(value);
                if (typeof(Guid).IsAssignableFrom(propertyValueType))
                {
                    var guidValue = (Guid)rowKeyValue;
                    return await callback(guidValue.AsRowKey());
                }
                if (typeof(IReferenceable).IsAssignableFrom(propertyValueType))
                {
                    var refValue = (IReferenceable)rowKeyValue;
                    if (refValue.IsDefaultOrNull())
                        return onFailure();
                    return await callback(refValue.id.AsRowKey());
                }
                if (typeof(string).IsAssignableFrom(propertyValueType))
                {
                    var stringValue = (string)rowKeyValue;
                    return await callback(stringValue);
                }
                return await callback(rowKeyValue.ToString());
            }
            return GetRowKey(
                rowKey =>
                {
                    var referencedEntityType = ReferenceType.IsDefaultOrNull() ?
                        propertyValueType.GetGenericArguments().First()
                        :
                        ReferenceType;
                    var partitionKey = value.StorageGetPartitionKey(); // GetPartitionKey(rowKey, value, memberInfo);
                    return repository.UpdateAsync<TResult>(rowKey, partitionKey,
                            referencedEntityType,
                        async (entity, saveAsync) =>
                        {
                            var fieldToModifyFieldInfo = referencedEntityType
                                .GetFields()
                                .Select(
                                    field =>
                                    {
                                        return field
                                            .GetAttributesInterface<IPersistInAzureStorageTables>()
                                            .Where(attr => attr.Name == this.ReferenceProperty)
                                            .First(
                                                (attr, next) => field,
                                                () => default(FieldInfo));
                                    })
                                .Where(v => !v.IsDefaultOrNull())
                                .First();
                            var valueToMutate = fieldToModifyFieldInfo.GetValue(entity);
                            var valueToMutateType = valueToMutate.GetType();
                            if (valueToMutateType.IsSubClassOfGeneric(typeof(IRefs<>)))
                            {
                                var references = valueToMutate as IReferences;
                                var idsOriginal = references.ids;
                                var rowKeyId = Guid.Parse(rowKeyRef);
                                if (idsOriginal.Contains(rowKeyId))
                                    return onSuccessWithRollback(() => 1.AsTask());

                                var ids = idsOriginal
                                    .Append(rowKeyId)
                                    .Distinct()
                                    .ToArray();
                                var refsInstantiatable = typeof(Refs<>)
                                    .MakeGenericType(valueToMutateType.GenericTypeArguments.First().AsArray());
                                var valueMutated = Activator.CreateInstance(refsInstantiatable, ids.AsArray());

                                fieldToModifyFieldInfo.SetValue(ref entity, valueMutated);

                                await saveAsync(entity);
                                Func<Task> rollback =
                                    async () =>
                                    {
                                        bool rolled = await repository.UpdateAsync<bool>(rowKey, partitionKey,
                                                referencedEntityType,
                                            async (entityRollback, saveRollbackAsync) =>
                                            {
                                                fieldToModifyFieldInfo.SetValue(ref entityRollback, valueToMutate);
                                                await saveRollbackAsync(entityRollback);
                                                return true;
                                            });
                                    };
                                return onSuccessWithRollback(rollback);
                            }

                            return onFailure();
                        });
                });
        }

        private class FaildModificationHandler<TResult> : IHandleFailedModifications<TResult>
        {
            internal MemberInfo member;
            internal Func<TResult> handler;

            public bool DoesMatchMember(MemberInfo[] membersWithFailures)
            {
                var doesMatchMember =  membersWithFailures
                    .Where(memberWithFailure => memberWithFailure.ContainsCustomAttribute<StorageLinkAttribute>(true))
                    .Where(memberWithFailure => memberWithFailure.Name == member.Name)
                    .Any();
                return doesMatchMember;
            }

            public TResult ModificationFailure(MemberInfo[] membersWithFailures)
            {
                var failureMember = membersWithFailures
                    .Where(membersWithFailure => membersWithFailure.ContainsCustomAttribute<StorageLinkAttribute>(true))
                    .First();
                return handler();
            }
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
                onSuccessWithRollback,
                onFailure);
        }

        public Task<TResult> ExecuteUpdateAsync<TEntity, TResult>(MemberInfo memberInfo, 
                string rowKeyRef, string partitionKeyRef, 
                TEntity valueExisting, IDictionary<string, EntityProperty> dictionaryExisting,
                TEntity valueUpdated, IDictionary<string, EntityProperty> dictionaryUpdated, 
                AzureTableDriverDynamic repository, 
            Func<Func<Task>, TResult> onSuccessWithRollback, 
            Func<TResult> onFailure)
        {
            // Since only updating the row/partition keys could force a change here, just ignroe
            return onSuccessWithRollback(
                () => true.AsTask()).AsTask();
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
                onSuccessWithRollback,
                onFailure);
        }
    }
}

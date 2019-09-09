﻿using BlackBarLabs;
using BlackBarLabs.Extensions;
using BlackBarLabs.Persistence.Azure.StorageTables;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Linq.Expressions;
using EastFive.Persistence.Azure.StorageTables.Driver;
using EastFive.Serialization;
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
    public class StorageConstraintUniqueAttribute : Attribute,
        IModifyAzureStorageTableSave
    {
        public interface IScope
        {
            string GetHashValue<TEntity>(MemberInfo memberInfo, TEntity value);
        }

        public class Scoping : Attribute, IScope
        {
            public string Scope { get; set; }
            public Scoping(string scope)
            {
                this.Scope = scope;
            }

            public string GetHashValue<TEntity>(MemberInfo memberInfo, TEntity value)
            {
                return this.Scope;
            }
        }

        public string Scope { get; set; }

        public string LookupTableName { get; set; }

        private string GetLookupTableName(MemberInfo memberInfo)
        {
            if (LookupTableName.HasBlackSpace())
                return this.LookupTableName;

            if(this.Scope.HasBlackSpace())
                return $"{memberInfo.DeclaringType.Name}{this.Scope}";

            return $"{memberInfo.DeclaringType.Name}{memberInfo.Name}";
        }

        [StorageTable]
        public struct StorageLookupTable
        {
            [RowKey]
            public string rowKey;

            [ParititionKey]
            public string partitionKey;

            [Storage]
            public string [] hashvalues;
        }

        protected virtual string GetHashKey<TEntity>(MemberInfo memberInfo, TEntity value)
        {
            var rowKeyValue = memberInfo.GetValue(value);
            var propertyValueType = memberInfo.GetMemberType();
            if (typeof(string).IsAssignableFrom(propertyValueType))
            {
                var stringValue = (string)rowKeyValue;
                return stringValue;
            }
            if (typeof(Guid).IsAssignableFrom(propertyValueType))
            {
                var guidValue = (Guid)rowKeyValue;
                return guidValue.ToString("N");
            }
            if (typeof(IReferenceable).IsAssignableFrom(propertyValueType))
            {
                var refValue = (IReferenceable)rowKeyValue;
                return refValue.id.ToString("N");
            }
            if (typeof(IReferenceableOptional).IsAssignableFrom(propertyValueType))
            {
                var referenceableOptional = (IReferenceableOptional)rowKeyValue;
                if (referenceableOptional.IsDefaultOrNull())
                    return "null";
                if (!referenceableOptional.HasValue)
                    return "null";
                return referenceableOptional.id.Value.ToString("N");
            }
            if (typeof(IReferences).IsAssignableFrom(propertyValueType))
            {
                var references = (IReferences)rowKeyValue;
                if (references.IsDefaultOrNull())
                    return string.Empty;
                return references.ids.Select(id => id.ToString("N")).Join(string.Empty);
            }
            var exMsg = $"{this.GetType().Name} is not implemented for type `{propertyValueType.FullName}`. " +
                $"Please override GetHashKey on `{this.GetType().FullName}`.";
            throw new NotImplementedException(exMsg);
        }

        protected string GetHashRowKey<TEntity>(MemberInfo memberInfo, TEntity value, out string [] hashKeys)
        {
            var baseKey = GetHashKey(memberInfo, value);
            if (Scope.IsNullOrWhiteSpace())
            {
                hashKeys = baseKey.AsArray();
                return baseKey.MD5HashString();
            }
            var scopedKeys = typeof(TEntity)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(member => member.ContainsAttributeInterface<IScope>())
                .Select(member =>
                    member
                        .GetAttributesInterface<IScope>()
                        .First()
                        .GetHashValue(member, value));
            hashKeys = baseKey.AsArray().Concat(scopedKeys).ToArray();
            return hashKeys.Join("|").MD5HashString();
        }

        public Task<TResult> ExecuteCreateAsync<TEntity, TResult>(MemberInfo memberInfo,
                string rowKeyRef, string partitionKeyRef,
                TEntity value, IDictionary<string, EntityProperty> dictionary,
                AzureTableDriverDynamic repository,
            Func<Func<Task>, TResult> onSuccessWithRollback,
            Func<TResult> onFailure)
        {
            var hashRowKey = GetHashRowKey(memberInfo, value, out string[] hashKeys);
            var hashPartitionKey = memberInfo.DeclaringType.Name;
            var tableName = GetLookupTableName(memberInfo);
            return repository.UpdateOrCreateAsync<StorageLookupTable, TResult>(
                    hashRowKey, hashPartitionKey,
                async (created, lookup, saveAsync) =>
                {
                    if (!created)
                        return onFailure();

                    lookup.hashvalues = hashKeys;
                    await saveAsync(lookup);
                    Func<Task<bool>> rollback =
                        async () =>
                        {
                            return await repository.DeleteAsync<StorageLookupTable, bool>(hashRowKey, hashPartitionKey,
                                    () => true,
                                    () => false,
                                    tableName: tableName);
                        };
                    return onSuccessWithRollback(rollback);
                },
                tableName: tableName);
        }

        public Task<TResult> ExecuteUpdateAsync<TEntity, TResult>(MemberInfo memberInfo, 
                string rowKeyRef, string partitionKeyRef, 
                TEntity valueExisting, IDictionary<string, EntityProperty> dictionaryExisting,
                TEntity valueUpdated, IDictionary<string, EntityProperty> dictionaryUpdated, 
                AzureTableDriverDynamic repository, 
            Func<Func<Task>, TResult> onSuccessWithRollback, 
            Func<TResult> onFailure)
        {
            var hashRowKey = GetHashRowKey(memberInfo, valueUpdated, out string[] hashKeys);
            var hashPartitionKey = memberInfo.DeclaringType.Name;
            var tableName = GetLookupTableName(memberInfo);
            return repository.UpdateOrCreateAsync<StorageLookupTable, TResult>(
                    hashRowKey, hashPartitionKey,
                async (created, lookup, saveAsync) =>
                {
                    if (!created)
                        return onFailure();


                    var rollbackMaybeTask = ExecuteDeleteAsync(memberInfo, rowKeyRef, partitionKeyRef,
                        valueExisting, dictionaryExisting,
                        repository,
                        (rb) => rb,
                        () => default);

                    lookup.hashvalues = hashKeys;
                    await saveAsync(lookup);
                    var rollbackMaybe = await rollbackMaybeTask;

                    Func<Task<bool>> rollback =
                        async () =>
                        {
                            if (!rollbackMaybe.IsDefaultOrNull())
                                await rollbackMaybe();
                            return await repository.DeleteAsync<StorageLookupTable, bool>(hashRowKey, hashPartitionKey,
                                    () => true,
                                    () => false,
                                    tableName: tableName);
                        };
                    return onSuccessWithRollback(rollback);
                },
                tableName: tableName);
        }

        public Task<TResult> ExecuteDeleteAsync<TEntity, TResult>(MemberInfo memberInfo, 
                string rowKeyRef, string partitionKeyRef,
                TEntity value, IDictionary<string, EntityProperty> dictionary, 
                AzureTableDriverDynamic repository,
            Func<Func<Task>, TResult> onSuccessWithRollback, 
            Func<TResult> onFailure)
        {
            var hashRowKey = GetHashRowKey(memberInfo, value, out string[] discard);
            var hashPartitionKey = memberInfo.DeclaringType.Name;
            var tableName = GetLookupTableName(memberInfo);
            return repository.DeleteAsync<StorageLookupTable, TResult>(
                    hashRowKey, hashPartitionKey,
                async (entity, deleteAsync) =>
                {
                    await deleteAsync();
                    return onSuccessWithRollback(
                        () => repository.CreateAsync(entity, (discardAgain) => true, () => false));
                },
                () => onSuccessWithRollback(() => 1.AsTask()),
                tableName: tableName);
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

        public static IHandleFailedModifications<TResult> ModificationFailureScoped<TResult>(
            string scoping,
            Func<TResult> handlerOnFailure)
        {
            return new FaildModificationHandler<TResult>()
            {
                member = default,
                handler = handlerOnFailure,
                scoping = scoping,
            };
        }

        private class FaildModificationHandler<TResult> : IHandleFailedModifications<TResult>
        {
            internal MemberInfo member;
            internal Func<TResult> handler;
            internal string scoping;

            public bool DoesMatchMember(MemberInfo[] membersWithFailures)
            {
                var doesMatchMember = membersWithFailures
                    .Where(memberWithFailure => memberWithFailure.ContainsCustomAttribute<StorageConstraintUniqueAttribute>(true))
                    .If(!member.IsDefaultOrNull(),
                        x => x.Where(memberWithFailure => memberWithFailure.Name == member.Name))
                    .If(scoping.HasBlackSpace(),
                        x => x.Where(memberWithFailure => memberWithFailure
                            .GetCustomAttribute<StorageConstraintUniqueAttribute>().Scope == scoping))
                    .Any();
                return doesMatchMember;
            }

            public TResult ModificationFailure(MemberInfo[] membersWithFailures)
            {
                return handler();
            }
        }
    }
}

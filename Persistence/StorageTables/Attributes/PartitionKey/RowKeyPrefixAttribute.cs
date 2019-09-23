using BlackBarLabs.Persistence.Azure;
using BlackBarLabs.Persistence.Azure.Attributes;
using EastFive.Linq.Expressions;
using EastFive.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Persistence.Azure.StorageTables
{
    public class RowKeyPrefixAttribute : Attribute,
        IModifyAzureStorageTablePartitionKey, IComputeAzureStorageTablePartitionKey,
        BlackBarLabs.Persistence.Azure.Attributes.StringKeyGenerator
    {
        private uint? charactersMaybe;
        public uint Characters
        {
            get
            {
                if (!charactersMaybe.HasValue)
                    return 2;
                return charactersMaybe.Value;
            }
            set
            {
                charactersMaybe = value;
            }
        }

        public string GeneratePartitionKey(string rowKey, object value, MemberInfo memberInfo)
        {
            return GetValue(rowKey);
        }

        public EntityType ParsePartitionKey<EntityType>(EntityType entity, string value, MemberInfo memberInfo)
        {
            // discard since generated from id
            return entity;
        }

        public string ComputePartitionKey(object refKey, MemberInfo memberInfo, string rowKey)
        {
            return GetValue(rowKey);
        }

        public string GetValue(string rowKey)
        {
            if (rowKey.IsNullOrWhiteSpace())
                return null;
            return rowKey.Substring(0, (int)this.Characters);
        }

        public IEnumerable<string> GeneratePartitionKeys(Type type, int skip, int top)
        {
            var formatter = $"X{this.Characters}";
            return Enumerable
                .Range(skip, top)
                .Select((paritionNum) => paritionNum.ToString(formatter).ToLower());
        }

        public IEnumerable<StringKey> GetKeys()
        {
            var formatter = $"X{this.Characters}";
            return Enumerable
                .Range(0, (int)Math.Pow(0x16, this.Characters))
                .Select((paritionNum) => new StringKey() { Equal = paritionNum.ToString(formatter).ToLower() });
        }
    }
}

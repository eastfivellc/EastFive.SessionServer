using BlackBarLabs.Persistence.Azure;
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
        IModifyAzureStorageTablePartitionKey, IComputeAzureStorageTablePartitionKey
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

        public string ComputePartitionKey<EntityType>(IRef<EntityType> refKey, string rowKey, MemberInfo memberInfo) where EntityType : IReferenceable
        {
            return GetValue(rowKey);
        }

        public string GetValue(string rowKey)
        {
            return rowKey.Substring(0, (int)this.Characters);
        }

        public IEnumerable<string> GeneratePartitionKeys(Type type, int skip, int top)
        {
            var formatter = $"X{this.Characters}";
            return Enumerable
                .Range(skip, top)
                .Select((paritionNum) => paritionNum.ToString(formatter).ToLower());
        }
    }
}

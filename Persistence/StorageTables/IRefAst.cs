using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Persistence.Azure.StorageTables
{
    public interface IRefAst
    {
        string RowKey { get; }
        string PartitionKey { get; }
    }

    public interface IRefAst<TEntity> : IRefAst
    {
    }

    public class RefAst : IRefAst
    {
        public string RowKey { get; private set; }

        public string PartitionKey { get; private set; }

        public RefAst(string rowKey, string partitionKey)
        {
            this.RowKey = rowKey;
            this.PartitionKey = partitionKey;
        }
    }

    public static class RefAstExtensions
    {
        public static IRefAst AsAstRef(this string rowKey, string partitionKey)
        {
            return new RefAst(rowKey, partitionKey);
        }
    }
}

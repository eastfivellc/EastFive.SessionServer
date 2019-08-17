using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Persistence.StorageTables
{
    public struct TableInformation
    {
        public long total;
        public long mismatchedRowKeys;
        public long mismatchedPartitionKeys;
        public IDictionary<string, long> partitions;
    }
}

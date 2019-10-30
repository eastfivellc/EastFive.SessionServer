using BlackBarLabs.Persistence.Azure.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Persistence.StorageTables.Backups
{
    public interface IBackupStorageType
    {
        IEnumerable<StorageResourceInfo> GetStorageResourceInfos(Type t);
    }

    public interface IBackupStorageMember
    {
        IEnumerable<StorageResourceInfo> GetStorageResourceInfos(MemberInfo t);
    }
}

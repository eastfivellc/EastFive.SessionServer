using EastFive.Persistence;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Azure
{
    
    //public class ASTFVCPropertyAttribute  : JsonPropertyAttribute, IPersistInAzureStorageTables
    //{
    //    public bool IsRowKey { get; set; }
    //    public Type ReferenceType { get; set; }
    //    public string ReferenceProperty { get; set; }

    //    public ASTFVCPropertyAttribute()
    //    {
    //    }

    //    public virtual KeyValuePair<string, EntityProperty>[] ConvertValue(object value, MemberInfo memberInfo)
    //    {
    //        var propertyName = this.PropertyName.IsNullOrWhiteSpace(
    //            () => memberInfo.Name,
    //            (text) => text);

    //        return StoragePropertyAttribute.ConvertValue(value, propertyName);
    //    }
    //}
}

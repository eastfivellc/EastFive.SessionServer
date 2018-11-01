using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Persistence
{
    public static class AzureStoreTablePersistenceExtensions
    {
        public static void AddAzureStoreTablePersistenceBindings(this EastFive.Api.HttpApplication httpApp)
        {
            httpApp.AddOrUpdateGenericBinding(
                typeof(IRef<>),
                (type, app, content, onBound, onFailure) =>
                {
                    var resourceType = type.GenericTypeArguments.First();
                    return httpApp.StringContentToType(typeof(Guid), content,
                        id =>
                        {
                            var instantiatableType = typeof(EastFive.Azure.Persistence.Ref<>).MakeGenericType(resourceType);
                            var instance = Activator.CreateInstance(instantiatableType, new object[] { id });
                            return onBound(instance);
                        },
                        (why) => onFailure(why));

                });
        }
    }
}

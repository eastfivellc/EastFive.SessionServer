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
                    return httpApp.Bind(typeof(Guid), content,
                        (id) =>
                        {
                            var instantiatableType = typeof(EastFive.Ref<>).MakeGenericType(resourceType);
                            var instance = Activator.CreateInstance(instantiatableType, new object[] { id });
                            return onBound(instance);
                        },
                        (why) => onFailure(why));
                });
            httpApp.AddOrUpdateGenericBinding(
                typeof(IRefs<>),
                (type, app, content, onBound, onFailure) =>
                {
                    var resourceType = type.GenericTypeArguments.First();
                    return httpApp.Bind(typeof(Guid[]), content,
                        (ids) =>
                        {
                            var instantiatableType = typeof(Refs<>).MakeGenericType(resourceType);
                            var instance = Activator.CreateInstance(instantiatableType, new object[] { ids });
                            return onBound(instance);
                        },
                        (why) => onFailure(why));

                });
        }
    }
}

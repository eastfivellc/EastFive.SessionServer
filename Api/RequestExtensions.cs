using System;
using System.Net.Http;
using System.Threading.Tasks;

using EastFive.Security.SessionServer.Persistence;

using EastFive.Api.Services;

namespace EastFive.Security.SessionServer
{
    public static class RequestExtensions
    {
        public static Context GetSessionServerContext()
        {
            var context = new EastFive.Security.SessionServer.Context(
                () => new EastFive.Security.SessionServer.Persistence.DataContext(Configuration.AppSettings.Storage));
            return context;
        }

        public static Context GetSessionServerContext(this HttpRequestMessage request)
        {
            return GetSessionServerContext();
        }
    }
}
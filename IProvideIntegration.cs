using System.Collections.Generic;

namespace EastFive.Security.SessionServer
{
    public interface IProvideIntegration
    {
        string GetDefaultName(IDictionary<string, string> extraParams);
    }
}
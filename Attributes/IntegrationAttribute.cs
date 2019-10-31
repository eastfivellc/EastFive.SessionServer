using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.ApplicationInsights.DataContracts;

using EastFive.Azure.Auth;

namespace EastFive.Api
{
    public class IntegrationAttribute : Attribute, IInstigatable
    {
        public Task<HttpResponseMessage> Instigate(HttpApplication httpApp,
                HttpRequestMessage request,
                ParameterInfo parameterInfo,
            Func<object, Task<HttpResponseMessage>> onSuccess)
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.AzureADB2C
{
    public static class AppSettings
    {
        public const string ClientId = "EastFive.AzureADB2C.ClientId";
        /// <summary>
        /// This can only be accessed when the Key is created
        /// AD UI -> App Registrations -> [APP] -> All Settings -> Keys -> [New Key]
        /// </summary>
        public const string ClientSecret = "EastFive.AzureADB2C.ClientSecret";
        public const string Tenant = "EastFive.AzureADB2C.Tenant";
    }
}


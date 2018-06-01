using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Security.SessionServer.Attributes
{
    public class IntegrationNameAttribute : Attribute
    {
        public string Name { get; set; }

        public IntegrationNameAttribute(string name)
        {
            this.Name = name;
        }
    }
}

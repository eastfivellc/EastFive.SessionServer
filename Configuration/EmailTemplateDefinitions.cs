using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Security.SessionServer.Configuration
{
    public static class EmailTemplateDefinitions
    {
        public const string InviteNewAccount = "EastFive.Security.SesionServer.Emails.InviteNewAccount";
        public const string LoginToken = "EastFive.Security.SesionServer.Emails.TokenLink";
        public const string InvitePassword = "EastFive.Security.SesionServer.Emails.InvitePassword";
    }
}

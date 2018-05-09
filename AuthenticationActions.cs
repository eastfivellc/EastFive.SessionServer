using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Security.SessionServer
{
    /// <summary>
    /// AuthenticationRequest can provide 1 of 3 purposes: 
    /// 1.) a signin attempt,
    /// 2.) an attempt to link an external account to an internal account
    ///     (add an additional credential)
    /// 3.) provide an integration to the external system
    /// </summary>
    public enum AuthenticationActions
    {
        /// <summary>
        /// A login attempt
        /// </summary>
        signin,

        /// <summary>
        /// An opportunity to connect ("link") a login from an external system to an internal account
        /// </summary>
        link,

        /// <summary>
        /// Integration that provides "access" to an external system's data
        /// </summary>
        access,
    }
}

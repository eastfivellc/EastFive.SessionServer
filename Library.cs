using System;
using System.Linq;
using System.Net;
using System.Configuration;
using System.Web.Http;
using System.Threading.Tasks;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

using Microsoft.IdentityModel.Tokens;

using BlackBarLabs;
using EastFive.Api.Services;
using BlackBarLabs.Api.Resources;
using System.Web.Http.Routing;

namespace EastFive.Security.SessionServer
{
    public static class Library
    {
        internal static IConfigureIdentityServer configurationManager;
    }
}

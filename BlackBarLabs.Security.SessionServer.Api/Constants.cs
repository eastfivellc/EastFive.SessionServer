using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace BlackBarLabs.Security.AuthorizationServer.API
{
    public static class Constants
    {
        public static class AppSettingKeys
        {
            public const string DebugShowFullErrors = "Debug.ShowFullErrors";
            public const string CspDefaultSrc = "CSP.DefaultSrc";
            public const string CspFontSrc = "CSP.FontSrc";
            public const string CspFrameSrc = "CSP.FrameSrc";
            public const string CspImageSrc = "CSP.ImageSrc";
            public const string CspReportEndPoint = "CSP.ReportEndPoint";
            public const string CspScriptSrc = "CSP.ScriptSrc";
            public const string CspStyleSrc = "CSP.StyleSrc";
            public const string SmtpPort = "Smtp.Port";
            public const string SmtpUserName = "Smtp.UserName";
            public const string SmtpPassword = "Smtp.Password";
            public const string SmtpHost = "Smtp.Host";
        }

        public static class Areas
        {
            public static IReadOnlyList<string> AreaNames = new[]
            {
                "~",
                "~/areas/admin",
                "~/areas/advertisers"
            };

            public static class AreaBundlePaths
            {
                public static IReadOnlyList<string> StylesheetPaths = new[]
                {
                    "{0}/content/app/css"
                };

                public static IReadOnlyList<string> FolderScriptPaths = new[]
                {
                    "{0}/content/app/scripts",
                    "{0}/content/app/scripts/config",
                    "{0}/content/app/scripts/controllers",
                    "{0}/content/app/scripts/directives",
                    "{0}/content/app/scripts/filters",
                    "{0}/content/app/scripts/routing",
                    "{0}/content/app/scripts/services",
                    "{0}/content/app/scripts/vendor"
                };
            }
        }

        public static class SystemConfiguration
        {
            public const string Context = "BlackBarLabs.Web.UI.Context";
            public const string PasswordPolicy = "PasswordToolkit.PasswordPolicy";
            public const string MessageCredential = "BlackBarLabs.Common.Services.Messaging.MessageCredential";
            public const string ConfigurationService = "BlackBarLabs.Common.Services.Configuration.ConfigurationService";
            public const string ResponseHeaderOptions = "ResponseHeaderOptions";
            public const string UserIdToken = "USERIDTOKEN";
            public const string NC2PostmasterEmail = "postmaster@nc2media.com";
        }

        public static class LoggingConnections
        {
            public static readonly string TextLogPath = Path.GetFullPath(new Uri(Assembly.GetExecutingAssembly().CodeBase).AbsolutePath);
        }

    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using EastFive.Linq;
using BlackBarLabs.Api;
using BlackBarLabs.Extensions;
using BlackBarLabs.Api.Resources;
using System.Web.Http.Routing;

namespace EastFive.Azure
{
    public class Application : EastFive.Api.HttpApplication
    {
        public Application()
            : base()
        {
            this.AddInstigator(typeof(Security.SessionServer.Context),
                (httpApp, request, parameterInfo, onCreatedSessionContext) => onCreatedSessionContext(this.AzureContext));
        }

        public virtual Web.Services.ISendMessageService SendMessageService { get => Web.Services.ServiceConfiguration.SendMessageService(); }
        

        public virtual Web.Services.ITimeService TimeService { get => Web.Services.ServiceConfiguration.TimeService(); }

        internal virtual WebId GetActorLink(Guid actorId, UrlHelper url)
        {
            return Security.SessionServer.Library.configurationManager.GetActorLink(actorId, url);
        }

        public Security.SessionServer.Context AzureContext
        {
            get
            {
                return new EastFive.Security.SessionServer.Context(
                    () => new EastFive.Security.SessionServer.Persistence.DataContext(
                        EastFive.Security.SessionServer.Configuration.AppSettings.Storage));
            }
        }

    }
}

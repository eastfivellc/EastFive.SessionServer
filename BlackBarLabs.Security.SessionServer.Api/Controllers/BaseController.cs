using System.Web.Http;
using System.Web.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Permissions;
using System.Runtime.Serialization;
using BlackBarLabs.Security.CredentialProvider.ImplicitCreation;
using BlackBarLabs.Security.Authorization;
using BlackBarLabs.Security.SessionServer.Persistence.Azure;

namespace BlackBarLabs.Security.AuthorizationServer.API.Controllers
{
    [ContentNegotiationController.AutoContentNegotiation]
    public class BaseController : BlackBarLabs.Api.Controllers.BaseController
    {
        #region Dependency Resolution
        private static readonly object DataContextLock = new object();

        ConfigurationServiceDictionary configurationServiceDictionary;
        private ConfigurationServiceDictionary ConfigurationServiceDictionary
        {
            get
            {
                return configurationServiceDictionary ?? (configurationServiceDictionary = (ConfigurationServiceDictionary)GlobalConfiguration.Configuration.Properties[Constants.SystemConfiguration.ConfigurationService]);
            }
        }

        Context dataContext;
        protected Context DataContext
        {
            get
            {
                if (dataContext != null) return dataContext;

                lock (DataContextLock)
                    if (dataContext == null)
                        return dataContext ?? (dataContext = GetContext());

                return dataContext;
            }
        }
        #endregion

        private static Context GetContext()
        {
            var context = new Context(() => new DataContext("Azure.Authorization.Storage"),
                (credentialValidationMethodType) =>
                {
                    switch(credentialValidationMethodType)
                    {
                        case CredentialValidationMethodTypes.Facebook:
                            return new Security.CredentialProvider.Facebook.FacebookCredentialProvider();
                        case CredentialValidationMethodTypes.Implicit:
                            return new ImplicitlyCreatedCredentialProvider();
                        case CredentialValidationMethodTypes.Voucher:
                            return new CredentialProvider.Voucher.VoucherCredentialProvider();
                        default:
                            break;
                    }
                    return new Security.CredentialProvider.OpenIdConnect.OpenIdConnectCredentialProvider();
                });
            return context;
        }

        protected override void Initialize(System.Web.Http.Controllers.HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);
        }
    }

    [Serializable]
    public class ConfigurationServiceDictionary : Dictionary<string, object>
    {
        #region Serialization

        protected ConfigurationServiceDictionary(SerializationInfo info, StreamingContext context) : base(info, context)
        {

        }

        public ConfigurationServiceDictionary()
        {

        }

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }
        #endregion

        public TOutput GetByKey<TOutput>(string key)
        {
            return (ContainsKey(key) && this[key] is TOutput) ? (TOutput)this[key] : default(TOutput);
        }
    }
}
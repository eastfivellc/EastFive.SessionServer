using BlackBarLabs.Extensions;
using EastFive.Security.CredentialProvider;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using EastFive.Security.SessionServer.Persistence;
using EastFive.Api.Services;
using System.Security.Claims;
using EastFive.Security.SessionServer;

namespace EastFive.Security.CredentialProvider.SAML
{
    [SessionServer.Attributes.IntegrationName("SAML")]
    public class SAMLProvider : IProvideLogin
    {
        private DataContext dataContext;
        
        internal const string SamlpResponseKey = "samlp:Response";
        internal const string SamlAssertionKey = "saml:Assertion";
        internal const string SamlSubjectKey = "saml:Subject";
        internal const string SamlNameIDKey = "saml:NameID";

        public SAMLProvider()
        {
            this.dataContext = new DataContext(SessionServer.Configuration.AppSettings.Storage);
        }

        [SessionServer.Attributes.IntegrationName("SAML")]
        public static Task<TResult> InitializeAsync<TResult>(
            Func<IProvideLogin, TResult> onProvideLogin,
            Func<IProvideAuthorization, TResult> onProvideAuthorization,
            Func<TResult> onProvideNothing,
            Func<string, TResult> onFailure)
        {
            return onProvideAuthorization(new SAMLProvider()).ToTask();
        }
        
        public async Task<TResult> RedeemTokenAsync<TResult>(IDictionary<string, string> tokens,
            Func<string, Guid?, Guid?, IDictionary<string, string>, TResult> onSuccess,
            Func<Guid?, IDictionary<string, string>, TResult> onUnauthenticated,
            Func<string, TResult> onInvalidCredentials,
            Func<string, TResult> onCouldNotConnect,
            Func<string, TResult> onUnspecifiedConfiguration,
            Func<string, TResult> onFailure)
        {
            return await EastFive.Web.Configuration.Settings.GetBase64Bytes(AppSettings.SAMLCertificate,
                async (certBuffer) =>
                {
                    var certificate = new X509Certificate2(certBuffer);
                    var m = ((RSACryptoServiceProvider)certificate.PrivateKey);
                    AsymmetricAlgorithm trustedSigner = m; // AsymmetricAlgorithm.Create(certificate.GetKeyAlgorithm()
                    var trustedSigners = default(AsymmetricAlgorithm) == trustedSigner ? null : trustedSigner.AsEnumerable();
                    
                    try
                    {
                        var nameId = tokens[SAMLProvider.SamlNameIDKey];

                        return EastFive.Web.Configuration.Settings.GetString(AppSettings.SAMLLoginIdAttributeName,
                            (attributeName) =>
                            {
                                //var attributes = assertion.Attributes
                                //    .Where(attribute => attribute.Name.CompareTo(attributeName) == 0)
                                //    .ToArray();
                                //if (attributes.Length == 0)
                                //    return invalidCredentials($"SAML assertion does not contain an attribute with name [{attributeName}] which is necessary to operate with this system");
                                //Guid authId;
                                //if (!Guid.TryParse(attributes[0].AttributeValue.First(), out authId))
                                //    return invalidCredentials("User's auth identifier is not a guid.");

                                var hash = SHA512.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(nameId));
                                var loginId = new Guid(hash.Take(16).ToArray());

                                return onSuccess(nameId, default(Guid?), loginId, new Dictionary<string, string>()); // TODO: Build this from params above
                            },
                            (why) => onUnspecifiedConfiguration(why));
                    } catch(Exception ex)
                    {
                        return await onInvalidCredentials("SAML Assertion parse and validate failed").ToTask();
                    }
                },
                (why) => onUnspecifiedConfiguration(why).ToTask());
        }

        #region IProvideLogin

        public Type CallbackController => typeof(SessionServer.Api.Controllers.SAMLRedirectController);

        public Uri GetSignupUrl(Guid state, Uri responseControllerLocation)
        {
            throw new NotImplementedException();
        }

        public Uri GetLogoutUrl(Guid state, Uri responseControllerLocation)
        {
            throw new NotImplementedException();
        }

        public Uri GetLoginUrl(Guid state, Uri responseControllerLocation)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> UserParametersAsync<TResult>(Guid actorId, System.Security.Claims.Claim[] claims, IDictionary<string, string> extraParams, Func<IDictionary<string, string>, IDictionary<string, Type>, IDictionary<string, string>, TResult> onSuccess)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}

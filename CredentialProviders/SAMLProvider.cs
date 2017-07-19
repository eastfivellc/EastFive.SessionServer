using BlackBarLabs.Extensions;
using EastFive.Security.CredentialProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace EastFive.Security.SessionServer.CredentialProvider.SAML
{
    public class SAMLProvider : IProvideCredentials
    {
        public async Task<TResult> RedeemTokenAsync<TResult>(string token, 
            Func<Guid, TResult> success,
            Func<string, TResult> invalidCredentials,
            Func<TResult> onAuthIdNotFound,
            Func<string, TResult> couldNotConnect)
        {
            return EastFive.Web.Configuration.Settings.GetBase64Bytes(AppSettings.SAMLCertificate,
                (certBuffer) =>
                {
                    var certificate = new X509Certificate2(certBuffer);
                    var m = ((RSACryptoServiceProvider)certificate.PrivateKey);
                    AsymmetricAlgorithm trustedSigner = m; // AsymmetricAlgorithm.Create(certificate.GetKeyAlgorithm()
                    var trustedSigners = default(AsymmetricAlgorithm) == trustedSigner ? null : trustedSigner.ToEnumerable();

                    var doc = new XmlDocument();
                    try
                    {
                        var docXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + token;
                        doc.LoadXml(docXml);
                        var samlXml = doc.DocumentElement;
                        var assertion = new SAML2.Saml20Assertion(samlXml, trustedSigners, false,
                            new SAML2.Config.Saml2Configuration
                            {
                            });
                        return EastFive.Web.Configuration.Settings.GetString(AppSettings.SAMLLoginIdAttributeName,
                            (attributeName) =>
                            {
                                var attributes = assertion.Attributes
                                    .Where(attribute => attribute.Name.CompareTo(attributeName) == 0)
                                    .ToArray();
                                if (attributes.Length == 0)
                                    return invalidCredentials($"SAML assertion does not contain an attribute with name [{attributeName}] which is necessary to operate with this system");
                                Guid authId;
                                if (!Guid.TryParse(attributes[0].AttributeValue.First(), out authId))
                                    return invalidCredentials("User's auth identifier is not a guid.");

                                return success(authId);
                            },
                            couldNotConnect);
                    } catch(Exception ex)
                    {
                        return invalidCredentials("SAML Assertion parse and validate failed");
                    }
                },
                couldNotConnect);
        }
    }
}

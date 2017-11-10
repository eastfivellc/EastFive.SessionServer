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

namespace EastFive.Security.SessionServer.CredentialProvider.SAML
{
    public class SAMLProvider : IProvideCredentials
    {
        private DataContext dataContext;

        public SAMLProvider(Persistence.DataContext context)
        {
            this.dataContext = context;
        }

        public async Task<TResult> RedeemTokenAsync<TResult>(string token, Dictionary<string, string> extraParams,
            Func<Guid, IDictionary<string, string>, TResult> onSuccess,
            Func<string, TResult> onInvalidCredentials,
            Func<TResult> onAuthIdNotFound,
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

                    //var doc = new XmlDocument();
                    try
                    {
                        var doc = XDocument.Parse(token); //or XDocument.Load(path)
                        string jsonText = JsonConvert.SerializeXNode(doc);
                        var dyn = JsonConvert.DeserializeObject<ExpandoObject>(jsonText);

                        var response = ((IDictionary<string, object>)dyn)["samlp:Response"];
                        var assertion = ((IDictionary<string, object>)response)["saml:Assertion"];
                        var subject = ((IDictionary<string, object>)assertion)["saml:Subject"];
                        var nameIdNode = ((IDictionary<string, object>)subject)["saml:NameID"];
                        var nameId = (string)((IDictionary<string, object>)nameIdNode)["#text"];
                        //var docXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + token;
                        //doc.LoadXml(token);

                        //var samlXml = doc.DocumentElement;
                        //var events = new System.Xml.Serialization.XmlDeserializationEvents();
                        //var serializer = new System.Xml.Serialization.XmlSerializer(typeof(Guid));
                        //serializer.Deserialize(new System.Xml.XmlNodeReader(samlXml), "utf-8", events);
                        //var assertionNode = (XmlElement)samlXml.SelectSingleNode("/*[local-name() ='Response']/*[local-name() ='Assertion']");
                        //var assertion = new SAML2.Saml20Assertion(assertionNode, trustedSigners, false,
                        //    new SAML2.Config.Saml2Configuration
                        //    {
                        //        AllowedAudienceUris = 
                        //    });

                        return await EastFive.Web.Configuration.Settings.GetString(AppSettings.SAMLLoginIdAttributeName,
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
                                var id = new Guid(hash.Take(16).ToArray());

                                return this.dataContext.CredentialMappings.FindTokenCredentialByTokenAsync(id,
                                    (inviteId, actorId, loginId) =>
                                    {
                                        if (!loginId.HasValue)
                                            return invalidCredentials("Token is not connected to an account");
                                        return onSuccess(loginId.Value, new Dictionary<string, string>()); // TODO: Build this from params above
                                    },
                                    () => invalidCredentials("Token does not exist"));
                            },
                            (why) => unspecifiedConfiguration(why).ToTask());
                    } catch(Exception ex)
                    {
                        return invalidCredentials("SAML Assertion parse and validate failed");
                    }
                },
                (why) => unspecifiedConfiguration(why).ToTask());
        }
    }
}

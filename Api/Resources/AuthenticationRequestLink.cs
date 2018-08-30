using System;
using System.Linq;
using System.Runtime.Serialization;
using BlackBarLabs.Api.Resources;
using Newtonsoft.Json;

namespace EastFive.Api.Azure.Credentials.Resources
{
    [DataContract]
    public class AuthenticationRequestLink : BlackBarLabs.Api.ResourceBase
    {
        #region Properties
        
        [DataMember]
        [JsonProperty(PropertyName = "secure_id")]
        public Guid SecureId { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "method")]
        public string Method { get; set; }
        
        public CredentialValidationMethodTypes CredentialValidationMethodType
        {
            get
            {
                Enum.TryParse(Method, out CredentialValidationMethodTypes credentialValidationMethodType);
                return credentialValidationMethodType;
            }
        }

        [DataMember]
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [DataMember]
        [JsonProperty(PropertyName = "image")]
        public Uri Image { get; set; }

        #endregion
    }
}

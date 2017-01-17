using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class Claim
    {
        [DataMember]
        public Guid Id { get; set; }

        [DataMember]
        public Guid AuthorizationId { get; set; }

        [DataMember]
        public Uri Issuer { get; set; }

        [DataMember]
        public Uri Type { get; set; }

        [DataMember]
        public string Value { get; set; }

        [DataMember]
        public string Signature { get; set; }
    }
}

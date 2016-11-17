using System;
using System.Runtime.Serialization;

namespace BlackBarLabs.Security.AuthorizationServer.API.Models.Filters
{
    [DataContract]
    public class SocialIdentityFilter
    {
        [DataMember]
        public Guid Id { get; set; }
    }
}
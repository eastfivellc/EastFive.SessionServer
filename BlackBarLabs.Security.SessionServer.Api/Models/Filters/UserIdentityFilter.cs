using System;
using System.Runtime.Serialization;

namespace BlackBarLabs.Security.AuthorizationServer.API.Models.Filters
{
    [DataContract]
    public class UserIdentityFilter
    {
        [DataMember]
        public Guid Id { get; set; }
    }
}
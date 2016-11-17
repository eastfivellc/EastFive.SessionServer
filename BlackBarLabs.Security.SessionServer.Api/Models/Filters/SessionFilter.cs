using System;
using System.Runtime.Serialization;

namespace BlackBarLabs.Security.AuthorizationServer.API.Models.Filters
{
    [DataContract]
    public class SessionFilter
    {
        [DataMember]
        public Guid Id { get; set; }
    }
}
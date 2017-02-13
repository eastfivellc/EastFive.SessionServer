using System.Runtime.Serialization;

namespace EastFive.Security.SessionServer.Api.Resources
{
    [DataContract]
    public class AuthHeaderProps
    {
        #region Properties
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Value { get; set; }
        #endregion
    }
}

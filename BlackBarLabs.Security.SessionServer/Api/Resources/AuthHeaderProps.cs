using System.Runtime.Serialization;

namespace BlackBarLabs.Security.Session
{
    [DataContract]
    public class AuthHeaderProps
    {
        public static class HeaderKeys
        {
            public const string JWTToken = "Authorization";
        }

        #region Properties
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Value { get; set; }
        #endregion
    }
}

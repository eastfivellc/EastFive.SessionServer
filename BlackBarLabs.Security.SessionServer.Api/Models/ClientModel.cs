namespace BlackBarLabs.Security.AuthorizationServer.API.Models
{
    public class ClientModel
    {
        public string Name { get; set; }
        public string RsaPublicKey { get; set; }
        public string CreatedKeyPair { get; set; }
    }
}
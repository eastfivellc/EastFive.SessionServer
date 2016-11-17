namespace BlackBarLabs.Security.AuthorizationServer.API.Models
{
    public class UpdatePassword
    {
        public string Email { get; set; }
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmNewPassword { get; set; }
    }
}
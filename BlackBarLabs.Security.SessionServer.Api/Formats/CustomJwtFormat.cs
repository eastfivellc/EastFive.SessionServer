using System;
//using BlackBarLabs.Security.Tokens;
//using Microsoft.Owin.Security;

//namespace BlackBarLabs.Security.AuthorizationServer.API.Formats
//{
//    public class CustomJwtFormat : ISecureDataFormat<AuthenticationTicket>
//    {
//        public string Protect(AuthenticationTicket data)
//        {
//            if (data == null)
//            {
//                throw new ArgumentNullException("data");
//            }
//            return JwtTools.CreateToken(data);
//        }

//        public AuthenticationTicket Unprotect(string protectedText)
//        {
//            throw new NotImplementedException();
//        }
//    }
//}
using System;

namespace BlackBarLabs.Security.AuthorizationServer.Exceptions
{
    public class InvalidCredentialsException : ArgumentException
    {
        public InvalidCredentialsException(string message) : base(message)
        {
        }
    }
}

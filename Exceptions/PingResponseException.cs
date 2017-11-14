using System;

namespace EastFive.Security.SessionServer.Exceptions
{
    public class PingResponseException : Exception
    {
        public PingResponseException()
            : base()
        {
        }

        public PingResponseException(string message)
            : base(message)
        {
        }

        public PingResponseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
using System;
using System.Threading.Tasks;

namespace BlackBarLabs.Security.SessionServer.Persistence
{
    public delegate Task<TResult> UpdateAuthenticationDelegate<TResult>(Guid storedAuthenticationId, Func<Guid, Task> saveNewAuthenticationId);
    public interface ISessions
    {
        Task<TResult> CreateAsync<TResult>(Guid sessionId, string refreshToken, Guid authorizationId,
            Func<TResult> success,
            Func<TResult> alreadyExists);

        /// <summary>
        /// Calls back the invocation method with currently stored authorization Id and 
        /// updates the authorization id with the return value of the method.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="authIdFunc"></param>
        /// <returns></returns>
        Task<TResult> UpdateAuthentication<TResult>(Guid sessionId, UpdateAuthenticationDelegate<TResult> authIdFunc, Func<TResult> notFound);

        /// <summary>
        /// Check if sessionId is stored in the database
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        Task<bool> DoesExistsAsync(Guid sessionId);
    }
}


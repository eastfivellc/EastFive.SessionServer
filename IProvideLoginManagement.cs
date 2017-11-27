using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EastFive.Security.SessionServer
{
    public struct LoginInfo
    {
        public Guid loginId;
        public string userName;
        public bool isEmail;
        public bool forceChange;
        public bool accountEnabled;
        internal string displayName;
        internal bool forceChangePassword;
    }


    public interface IProvideLoginManagement
    {
        Task<TResult> CreateAuthorizationAsync<TResult>(string displayName,
            string userId, bool isEmail, string secret, bool forceChange,
            Func<Guid, TResult> onSuccess,
            Func<Guid, TResult> usernameAlreadyInUse,
            Func<TResult> onPasswordInsufficent,
            Func<string, TResult> onServiceNotAvailable,
            Func<TResult> onServiceNotSupported,
            Func<string, TResult> onFailure);

        Task<TResult> GetAuthorizationAsync<TResult>(Guid loginId,
            Func<LoginInfo, TResult> onSuccess,
            Func<TResult> onNotFound,
            Func<string, TResult> onServiceNotAvailable,
            Func<TResult> onServiceNotSupported,
            Func<string, TResult> onFailure);

        Task<TResult> GetAllAuthorizationsAsync<TResult>(
            Func<LoginInfo[], TResult> onFound,
            Func<string, TResult> onServiceNotAvailable,
            Func<TResult> onServiceNotSupported,
            Func<string, TResult> onFailure);

        Task<TResult> UpdateAuthorizationAsync<TResult>(Guid loginId, string password, bool forceChange,
            Func<TResult> onSuccess,
            Func<string, TResult> onServiceNotAvailable,
            Func<TResult> onServiceNotSupported,
            Func<string, TResult> onFailure);

        Task<TResult> DeleteAuthorizationAsync<TResult>(Guid loginId,
            Func<TResult> onSuccess,
            Func<string, TResult> onServiceNotAvailable,
            Func<TResult> onServiceNotSupported,
            Func<string, TResult> onFailure);
    }
}

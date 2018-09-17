using BlackBarLabs.Extensions;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using EastFive.Security.SessionServer.Persistence;
using EastFive.Api.Services;
using EastFive.Security.SessionServer;

namespace EastFive.Api.Azure.Credentials
{
    [Attributes.IntegrationName("Token")]
    public class TokenCredentialProvider : IProvideAuthorization, IProvideLoginManagement
    {
        private const string subjectIdKey = "loginId";

        private Security.SessionServer.Persistence.DataContext dataContext;

        public TokenCredentialProvider()
        {
            this.dataContext = new DataContext(Security.SessionServer.Configuration.AppSettings.Storage);
        }

        [Attributes.IntegrationName("Token")]
        public static Task<TResult> InitializeAsync<TResult>(
            Func<IProvideAuthorization, TResult> onProvideAuthorization,
            Func<TResult> onProvideNothing,
            Func<string, TResult> onFailure)
        {
            return onProvideAuthorization(new TokenCredentialProvider()).ToTask();
        }

        public CredentialValidationMethodTypes Method => CredentialValidationMethodTypes.Token;

        public Type CallbackController => typeof(Controllers.TokenController);

        public Task<TResult> RedeemTokenAsync<TResult>(IDictionary<string, string> extraParams,
            Func<string, Guid?, Guid?, IDictionary<string, string>, TResult> onSuccess,
            Func<Guid?, IDictionary<string, string>, TResult> onUnauthenticated,
            Func<string, TResult> onInvalidCredentials,
            Func<string, TResult> onCouldNotConnect,
            Func<string, TResult> onUnspecifiedConfiguration,
            Func<string, TResult> onFailure)
        {
            var token = string.Empty; // TODO: Find value from URL generator
            return this.dataContext.CredentialMappings.FindTokenCredentialByTokenAsync(Guid.Parse(token),
                (inviteId, actorId, loginId) =>
                {
                    if (!loginId.HasValue)
                        return onInvalidCredentials("Token is not connected to an account");

                    var subjectId = loginId.Value.ToString("N");
                    return onSuccess(subjectId, default(Guid?), loginId.Value,
                        new Dictionary<string, string>()
                        {
                            { TokenCredentialProvider.subjectIdKey, subjectId  }
                        });
                },
                () => onInvalidCredentials("The token does not exists"));
        }

        public TResult ParseCredentailParameters<TResult>(IDictionary<string, string> tokens,
            Func<string, Guid?, Guid?, TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            var subjectId = tokens[subjectIdKey];
            var loginIdMaybe = default(Guid?);
            if (Guid.TryParse(subjectId, out Guid loginId))
                loginIdMaybe = loginId;

            return onSuccess(subjectId, default(Guid?), loginIdMaybe);
        }

        #region IProvideLoginManagement

        public Task<TResult> CreateAuthorizationAsync<TResult>(string displayName, string userId, bool isEmail, string secret, bool forceChange, Func<Guid, TResult> onSuccess, Func<Guid, TResult> usernameAlreadyInUse, Func<TResult> onPasswordInsufficent, Func<string, TResult> onServiceNotAvailable, Func<TResult> onServiceNotSupported, Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> GetAuthorizationAsync<TResult>(Guid loginId, 
            Func<LoginInfo, TResult> onSuccess, 
            Func<TResult> onNotFound, 
            Func<string, TResult> onServiceNotAvailable, 
            Func<TResult> onServiceNotSupported, 
            Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> GetAllAuthorizationsAsync<TResult>(Func<LoginInfo[], TResult> onFound, Func<string, TResult> onServiceNotAvailable, Func<TResult> onServiceNotSupported, Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> UpdateAuthorizationAsync<TResult>(Guid loginId, string password, bool forceChange, Func<TResult> onSuccess, Func<string, TResult> onServiceNotAvailable, Func<TResult> onServiceNotSupported, Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> UpdateEmailAsync<TResult>(Guid loginId, string email, Func<TResult> onSuccess, Func<string, TResult> onServiceNotAvailable, Func<TResult> onServiceNotSupported, Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> DeleteAuthorizationAsync<TResult>(Guid loginId, Func<TResult> onSuccess, Func<string, TResult> onServiceNotAvailable, Func<TResult> onServiceNotSupported, Func<string, TResult> onFailure)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> UserParametersAsync<TResult>(Guid actorId, System.Security.Claims.Claim[] claims, IDictionary<string, string> extraParams, Func<IDictionary<string, string>, IDictionary<string, Type>, IDictionary<string, string>, TResult> onSuccess)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}

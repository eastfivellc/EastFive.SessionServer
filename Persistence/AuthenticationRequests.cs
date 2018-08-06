using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

using BlackBarLabs.Collections.Async;
using BlackBarLabs.Persistence.Azure;
using BlackBarLabs.Persistence.Azure.StorageTables;
using BlackBarLabs.Extensions;
using BlackBarLabs.Persistence;
using BlackBarLabs.Persistence.Azure.Extensions;
using BlackBarLabs.Linq;
using BlackBarLabs;
using EastFive.Extensions;

namespace EastFive.Security.SessionServer.Persistence
{
    public struct AuthenticationRequest
    {
        public Guid id;
        public string method;
        public AuthenticationActions action;
        public Guid? authorizationId;
        public IDictionary<string, string> extraParams;
        public string token;
        public Uri redirect;
        public Uri redirectLogout;
        internal DateTime? Deleted;
    }

    public class AuthenticationRequests
    {
        private AzureStorageRepository repository;
        private DataContext context;

        public AuthenticationRequests(DataContext context, AzureStorageRepository repository)
        {
            this.repository = repository;
            this.context = context;
        }

        public async Task<TResult> CreateAsync<TResult>(Guid authenticationRequestId,
                string method, AuthenticationActions action,
                Uri redirectUrl, Uri redirectLogoutUrl,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyExists)
        {
            var doc = new Documents.AuthenticationRequestDocument
            {
                Method = method,
                Action = Enum.GetName(typeof(AuthenticationActions), action),
                RedirectUrl = redirectUrl.IsDefault()?
                    default(string)
                    :
                    redirectUrl.AbsoluteUri,
                RedirectLogoutUrl = redirectLogoutUrl.IsDefault() ?
                    default(string)
                    :
                    redirectLogoutUrl.AbsoluteUri,
            };
            return await this.repository.CreateAsync(authenticationRequestId, doc,
                () => onSuccess(),
                () => onAlreadyExists());
        }

        public async Task<TResult> CreateAsync<TResult>(Guid authenticationRequestId,
                string method, AuthenticationActions action,
                Guid actorLinkId, string token, Uri redirectUrl, Uri redirectLogoutUrl,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyExists)
        {
            var doc = new Documents.AuthenticationRequestDocument
            {
                Method = method,
                Action = Enum.GetName(typeof(AuthenticationActions), action),
                LinkedAuthenticationId = actorLinkId,
                Token = token,
                RedirectUrl = redirectUrl.IsDefault() ?
                    default(string)
                    :
                    redirectUrl.AbsoluteUri,
                RedirectLogoutUrl = redirectLogoutUrl.IsDefault() ?
                    default(string)
                    :
                    redirectLogoutUrl.AbsoluteUri,
            };
            return await this.repository.CreateAsync(authenticationRequestId, doc,
                () => onSuccess(),
                () => onAlreadyExists());
        }

        public async Task<TResult> CreateAsync<TResult>(Guid authenticationRequestId,
                CredentialValidationMethodTypes method, AuthenticationActions action,
                Guid actorLinkId, string token, Uri redirectUrl, Uri redirectLogoutUrl,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyExists)
        {
            var doc = new Documents.AuthenticationRequestDocument
            {
                Method = Enum.GetName(typeof(CredentialValidationMethodTypes), method),
                Action = Enum.GetName(typeof(AuthenticationActions), action),
                LinkedAuthenticationId = actorLinkId,
                Token = token,
                RedirectUrl = redirectUrl.IsDefault() ?
                    default(string)
                    :
                    redirectUrl.AbsoluteUri,
                RedirectLogoutUrl = redirectLogoutUrl.IsDefault() ?
                    default(string)
                    :
                    redirectLogoutUrl.AbsoluteUri,
            };
            return await this.repository.CreateAsync(authenticationRequestId, doc,
                () => onSuccess(),
                () => onAlreadyExists());
        }

        public async Task<TResult> FindByIdAsync<TResult>(Guid authenticationRequestId,
            Func<AuthenticationRequest, TResult> onSuccess,
            Func<TResult> onNotFound)
        {
            return await repository.FindByIdAsync(authenticationRequestId,
                (Documents.AuthenticationRequestDocument document) =>
                    onSuccess(Convert(document)),
                () => onNotFound());
        }

        public async Task<TResult> UpdateAsync<TResult>(Guid authenticationRequestId,
            Func<AuthenticationRequest, Func<Guid, string, IDictionary<string, string>, Task>, Task<TResult>> onFound,
            Func<TResult> onNotFound)
        {
            return await this.repository.UpdateAsync<Documents.AuthenticationRequestDocument, TResult>(authenticationRequestId,
                async (document, saveAsync) =>
                {
                    return await onFound(Convert(document),
                        async (linkedAuthenticationId, token, extraParams) =>
                        {
                            document.LinkedAuthenticationId = linkedAuthenticationId;
                            document.Token = token;
                            document.SetExtraParams(extraParams);
                            await saveAsync(document);
                        });
                },
                () =>
                {
                    return onNotFound();
                });
        }

        public async Task<TResult> DeleteAsync<TResult>(Guid authenticationRequestId,
            Func<AuthenticationRequest, Func<Task>, Task<TResult>> onFound,
            Func<TResult> onNotFound)
        {
            return await this.repository.UpdateAsync<Documents.AuthenticationRequestDocument, TResult>(authenticationRequestId,
                async (document, saveAsync) =>
                {
                    return await onFound(Convert(document),
                        async () =>
                        {
                            document.Deleted = DateTime.UtcNow;
                            await saveAsync(document);
                        });
                },
                () => onNotFound());
        }

        public async Task<TResult> DeleteByIdAsync<TResult>(Guid authenticationRequestId,
            Func<AuthenticationRequest, Func<Task>, Task<TResult>> onSuccess,
            Func<TResult> onNotFound)
        {
            return await repository.DeleteIfAsync<Documents.AuthenticationRequestDocument, TResult>(authenticationRequestId,
                async (document, deleteAsync) =>
                {
                    return await onSuccess(Convert(document), deleteAsync);
                },
                () => onNotFound());
        }

        internal static AuthenticationRequest Convert(Documents.AuthenticationRequestDocument document)
        {
            return new AuthenticationRequest
            {
                id = document.Id,
                method = document.Method,
                action = (AuthenticationActions)Enum.Parse(typeof(AuthenticationActions), document.Action, true),
                authorizationId = document.LinkedAuthenticationId,
                token = document.Token,
                extraParams = document.GetExtraParams(),
                redirect = document.RedirectUrl.IsNullOrWhiteSpace()?
                    default(Uri)
                    :
                    new Uri(document.RedirectUrl),
                redirectLogout = document.RedirectLogoutUrl.IsNullOrWhiteSpace() ?
                    default(Uri)
                    :
                    new Uri(document.RedirectLogoutUrl),
                Deleted = document.Deleted,
            };
        }
    }
}
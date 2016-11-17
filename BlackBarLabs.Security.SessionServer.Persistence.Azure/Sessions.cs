using System;
using System.Threading.Tasks;
using BlackBarLabs.Persistence.Azure.StorageTables;

namespace BlackBarLabs.Security.SessionServer.Persistence.Azure
{
    internal class Sessions : Persistence.ISessions
    {
        private AzureStorageRepository repository;
        public Sessions(AzureStorageRepository repository)
        {
            this.repository = repository;
        }

        #region Actionables

        public async Task<TResult> CreateAsync<TResult>(Guid sessionId, string refreshToken, Guid authorizationId,
            Func<TResult> success,
            Func<TResult> alreadyExists)
        {
            var document = new Documents.SessionDocument()
            {
                SessionId = sessionId,
                RefreshToken = refreshToken,
                AuthorizationId = authorizationId,
            };
            return await this.repository.CreateAsync(sessionId, document,
                () => success(),
                () => alreadyExists());
        }

        public async Task<bool> DoesExistsAsync(Guid sessionId)
        {
            var sessionDocument = await repository.FindById<Documents.SessionDocument>(sessionId);
            return null != sessionDocument;
        }

        #endregion


        public async Task UpdateAuthentication(Guid sessionId, Func<Guid, Guid> authIdFunc)
        {
            await repository.UpdateAtomicAsync<Documents.SessionDocument>(sessionId, (sessionDoc) =>
                {
                    sessionDoc.AuthorizationId = authIdFunc.Invoke(sessionDoc.AuthorizationId);
                    return sessionDoc;
                });
        }

        public async Task<TResult> UpdateAuthentication<TResult>(Guid sessionId,
            UpdateAuthenticationDelegate<TResult> found,
            Func<TResult> notFound)
        {
            var result = await repository.UpdateAsync<Documents.SessionDocument, TResult>(sessionId,
                async (sessionDoc, onSave) =>
                {
                    return await found(sessionDoc.AuthorizationId,
                        async (updatedAuthId) =>
                        {
                            sessionDoc.AuthorizationId = updatedAuthId;
                            await onSave(sessionDoc);
                        });
                },
                () => notFound());
            return result;
        }
    }
}

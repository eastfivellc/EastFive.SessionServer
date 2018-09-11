using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using BlackBarLabs.Persistence.Azure;
using BlackBarLabs.Persistence.Azure.StorageTables;
using BlackBarLabs.Web;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Collections.Generic;
using EastFive.Serialization;

namespace EastFive.Api.Azure.Credentials
{
    [Serializable]
    [DataContract]
    public class CredentialProcessDocument : TableEntity
    {
        public CredentialProcessDocument()
        {
        }

        public CredentialProcessDocument(string id)
        {
            RowKey = id;
            PartitionKey = RowKey.GeneratePartitionKey();
        }

        public Guid Id => Guid.Parse(this.RowKey);

        public Guid SessionId { get; set; }

        public Guid AuthorizationId { get; set; }
        
        public DateTime Time { get; set; }

        public string Message { get; set; }

        public string Method { get; set; }

        public string Token { get; set; }

        public string RefreshToken { get; set; }

        public string Action { get; set; }

        public string Provider { get; set; }

        public string RedirectUrl { get; set; }

        public byte[] ValuesRedirect { get; set; }

        internal IDictionary<string, string> GetValuesRedirect()
        {
            return ValuesRedirect.FromByteArray(
                (keyBytes) => System.Text.Encoding.UTF8.GetString(keyBytes),
                (valueBytes) => System.Text.Encoding.UTF8.GetString(valueBytes));
        }

        internal void SetValuesRedirect(IDictionary<string, string> extraParams)
        {
            ValuesRedirect = extraParams.ToByteArray(
                (key) => System.Text.Encoding.UTF8.GetBytes(key),
                (value) => System.Text.Encoding.UTF8.GetBytes(value));
        }

        public byte[] ValuesCredential { get; set; }

        internal IDictionary<string, string> GetValuesCredential()
        {
            return ValuesCredential.FromByteArray(
                (keyBytes) => System.Text.Encoding.UTF8.GetString(keyBytes),
                (valueBytes) => System.Text.Encoding.UTF8.GetString(valueBytes));
        }

        internal void SetValuesCredential(IDictionary<string, string> extraParams)
        {
            ValuesCredential = extraParams.ToByteArray(
                (key) => System.Text.Encoding.UTF8.GetBytes(key),
                (value) => System.Text.Encoding.UTF8.GetBytes(value));
        }
        
        internal static Task CreateAsync(Guid id,
            CredentialProcessDocument doc, AzureStorageRepository azureStorageRepository)
        {
            return azureStorageRepository.CreateAsync(id, doc,
                () => true,
                () => false);
        }

        public static async Task<TResult> UpdateAsync<TResult>(Guid id,
            Func<CredentialProcessDocument, Func<CredentialProcessDocument, Task>, Task<TResult>> onSuccess,
            AzureStorageRepository repo)
        {
            return await repo.UpdateAsync<CredentialProcessDocument, TResult>(id,
                (doc, saveAsync) => onSuccess(doc, (docUpdate) => saveAsync(docUpdate)),
                () => throw new NotImplementedException());
        }
    }
}

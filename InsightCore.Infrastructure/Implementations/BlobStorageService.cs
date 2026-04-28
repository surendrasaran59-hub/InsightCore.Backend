using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using InsightCore.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InsightCore.Infrastructure.Implementations
{
    /// <summary>
    /// Azure Blob Storage implementation of <see cref="IBlobStorageService"/>.
    /// Reads the connection string from IConfiguration key "AzureStorage:ConnectionString".
    /// </summary>
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobServiceClient _serviceClient;
        private readonly ILogger<BlobStorageService> _logger;

        public BlobStorageService(
            IConfiguration configuration,
            ILogger<BlobStorageService> logger)
        {
            _logger = logger;

            var connectionString = configuration["insightcore-blob-dev"]
                ?? throw new InvalidOperationException(
                    "AzureStorage:ConnectionString is not configured. " +
                    "Add it to appsettings.json or Azure Key Vault.");

            _serviceClient = new BlobServiceClient(connectionString);
        }

        // ── Upload ──────────────────────────────────────────────────────────────
        public async Task<string> UploadFileAsync(
            string containerName,
            string blobName,
            Stream stream,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            var containerClient = await GetOrCreateContainerAsync(containerName, cancellationToken);
            var blobClient = containerClient.GetBlobClient(blobName);

            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                // Overwrite if the same blob name already exists (idempotent re-upload)
                TransferOptions = new Azure.Storage.StorageTransferOptions
                {
                    MaximumConcurrency = 4,
                    MaximumTransferSize = 4 * 1024 * 1024 // 4 MB chunks
                }
            };

            _logger.LogInformation(
                "Uploading blob '{BlobName}' to container '{Container}'.",
                blobName, containerName);

            await blobClient.UploadAsync(stream, uploadOptions, cancellationToken);

            _logger.LogInformation("Blob '{BlobName}' uploaded successfully.", blobName);

            // Return the blob URI (without SAS — add SAS generation here if private container)
            return blobClient.Uri.ToString();
        }

        // ── Delete ──────────────────────────────────────────────────────────────
        public async Task<bool> DeleteFileAsync(
            string containerName,
            string blobName,
            CancellationToken cancellationToken = default)
        {
            var containerClient = _serviceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var response = await blobClient.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Delete blob '{BlobName}' from '{Container}': deleted={Deleted}",
                blobName, containerName, response.Value);

            return response.Value;
        }

        // ── Exists ──────────────────────────────────────────────────────────────
        public async Task<bool> ExistsAsync(
            string containerName,
            string blobName,
            CancellationToken cancellationToken = default)
        {
            var containerClient = _serviceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            var response = await blobClient.ExistsAsync(cancellationToken);
            return response.Value;
        }

        // ── Private helpers ─────────────────────────────────────────────────────
        private async Task<BlobContainerClient> GetOrCreateContainerAsync(
            string containerName,
            CancellationToken cancellationToken)
        {
            var containerClient = _serviceClient.GetBlobContainerClient(containerName);

            // Creates the container only if it doesn't already exist
            await containerClient.CreateIfNotExistsAsync(
                PublicAccessType.None,   // Keep blobs private
                cancellationToken: cancellationToken);

            return containerClient;
        }
    }
}
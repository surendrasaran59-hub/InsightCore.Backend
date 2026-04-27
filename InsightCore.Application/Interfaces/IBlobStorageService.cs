namespace InsightCore.Application.Interfaces
{
    /// <summary>
    /// Abstracts Azure Blob Storage operations.
    /// Implemented in InsightCore.Infrastructure.
    /// </summary>
    public interface IBlobStorageService
    {
        /// <summary>
        /// Uploads a stream to the specified Blob Storage container.
        /// </summary>
        /// <param name="containerName">Target Blob container name.</param>
        /// <param name="blobName">Full blob path/name within the container.</param>
        /// <param name="stream">File content stream (must be readable and seeked to 0).</param>
        /// <param name="contentType">MIME type to store as blob metadata.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The public or SAS URI of the uploaded blob.</returns>
        Task<string> UploadFileAsync(
            string containerName,
            string blobName,
            Stream stream,
            string contentType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a blob if it exists.
        /// </summary>
        Task<bool> DeleteFileAsync(
            string containerName,
            string blobName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks whether a blob exists in the specified container.
        /// </summary>
        Task<bool> ExistsAsync(
            string containerName,
            string blobName,
            CancellationToken cancellationToken = default);
    }
}
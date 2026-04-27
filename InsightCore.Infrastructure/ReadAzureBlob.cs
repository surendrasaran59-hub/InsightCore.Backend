using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InsightCore.Infrastructure
{
    public class ReadAzureBlob
    {
        public static Stream BlobMemoryStream(string connectionString, string containerName, string blobName)
        {
            BlobClient blobClient = new BlobClient(connectionString, containerName, blobName);

            var response = blobClient.DownloadStreaming();
            var stream = response.Value.Content;

            return stream;
        }
    }
}

using InsightCore.Application;
using InsightCore.Infrastructure;
using Microsoft.AspNetCore.Http;
using Azure.Storage.Blobs;
using InsightCore.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InsightCore.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataModelUploadController : ControllerBase
    {
        // Allowed MIME types 
        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", // .xlsx
            "application/vnd.ms-excel",                                           // .xls
            "text/csv",                                                            // .csv
            "application/csv",
            "text/plain",      // some browsers send this for .csv
        };

        // ── Magic-byte signatures ───────────────────────────────────────────────
        // XLSX / XLS share the PK ZIP header (xlsx) or OLE2 header (xls)
        private static readonly byte[] ZipMagic = { 0x50, 0x4B, 0x03, 0x04 };   // PK.. (xlsx)
        private static readonly byte[] Ole2Magic = { 0xD0, 0xCF, 0x11, 0xE0 };   // OLE2 (xls)
        // CSV = plain text; we validate it has at least one comma or is printable ASCII/UTF-8

        private readonly IBlobStorageService _blobStorage;
        private readonly string _containerName;
        private readonly ILogger<DataModelUploadController> _logger;

        public DataModelUploadController(
            IBlobStorageService blobStorage,
            ILogger<DataModelUploadController> logger, 
            IConfiguration configuration)
        {
            _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _containerName = configuration.GetValue<string>("BlobStorage:ContainerName");

            if (string.IsNullOrWhiteSpace(_containerName))
                throw new InvalidOperationException("BlobStorage:ContainerName is missing in configuration.");
        }

        // ── POST /api/datamodelupload/upload ────────────────────────────────────
        /// <summary>
        /// Accepts a file upload, validates it and stores it in Azure Blob Storage.
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(52_428_800)] // 50 MB
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Upload(
            [FromForm] int clientId,
            [FromForm] IFormFile file,
            CancellationToken cancellationToken)
        {
            // 1.. MIME type validation 
            if (!AllowedMimeTypes.Contains(file.ContentType))
            {
                _logger.LogWarning("Upload rejected — disallowed MIME type: {Mime}", file.ContentType);
                return StatusCode(StatusCodes.Status415UnsupportedMediaType,
                    new { message = $"File type '{file.ContentType}' is not allowed. Upload Excel (.xlsx/.xls) or CSV files only." });
            }

            // 2. Byte-stream (magic-byte) validation 
            await using var stream = file.OpenReadStream();
            var (isValid, streamValidationError) = await ValidateFileStreamAsync(stream, file.FileName, cancellationToken);

            if (!isValid)
            {
                _logger.LogWarning("Upload rejected — stream validation failed for '{File}': {Reason}", file.FileName, streamValidationError);
                return BadRequest(new { message = streamValidationError });
            }

            // Reset stream position after validation reads
            stream.Seek(0, SeekOrigin.Begin);

            // 3. Delegate to Blob Storage
            try
            {
                var blobName = BuildBlobName(clientId, file.FileName);

                var blobUri = await _blobStorage.UploadFileAsync(
                    containerName: _containerName, //"datamodel-uploads",
                    blobName: blobName,
                    stream: stream,
                    contentType: file.ContentType,
                    cancellationToken);

                _logger.LogInformation(
                    "File '{BlobName}' uploaded for ClientID {ClientId}. URI: {Uri}",
                    blobName, clientId, blobUri);

                //var serviceBusClient = new ServiceBusClient(connectionString);
                //var sender = serviceBusClient.CreateSender("validation-queue");
                //var message = new ServiceBusMessage { Body = BinaryData.FromString(file.FileName), ApplicationProperties = { ["ClientId"] = clientId } };
                //await sender.SendMessageAsync(message);
                //return Ok();

                return Ok(new
                {
                    message = "File uploaded and validated successfully.",
                    blobUri,
                    blobName,
                    clientId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Blob Storage upload failed for ClientID {ClientId}, file '{File}'.", clientId, file.FileName);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while storing the file. Please try again." });
            }
        }

        // ── Private helpers ─────────────────────────────────────────────────────

        private static async Task<(bool IsValid, string? ErrorMessage)> ValidateFileStreamAsync(
            Stream stream,
            string fileName,
            CancellationToken cancellationToken)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            // Read first 8 bytes for magic-byte check
            var header = new byte[8];
            var bytesRead = await stream.ReadAsync(header, 0, header.Length, cancellationToken);

            if (bytesRead < 4)
                return (false, "File is too small to be a valid Excel or CSV document.");

            return ext switch
            {
                ".xlsx" => ValidateXlsx(header),
                ".xls" => ValidateXls(header),
                ".csv" => ValidateCsv(header, bytesRead),
                _ => (false, $"Unsupported file extension '{ext}'.")
            };
        }

        private static (bool, string?) ValidateXlsx(byte[] header)
        {
            // .xlsx is a ZIP archive — must start with PK\x03\x04
            if (header[0] == ZipMagic[0] && header[1] == ZipMagic[1] &&
                header[2] == ZipMagic[2] && header[3] == ZipMagic[3])
                return (true, null);

            return (false, "File does not appear to be a valid .xlsx file (invalid ZIP signature).");
        }

        private static (bool, string?) ValidateXls(byte[] header)
        {
            // .xls is an OLE2 compound document — must start with D0 CF 11 E0
            if (header[0] == Ole2Magic[0] && header[1] == Ole2Magic[1] &&
                header[2] == Ole2Magic[2] && header[3] == Ole2Magic[3])
                return (true, null);

            // Some newer tools save xlsx with .xls extension — accept ZIP too
            if (header[0] == ZipMagic[0] && header[1] == ZipMagic[1] &&
                header[2] == ZipMagic[2] && header[3] == ZipMagic[3])
                return (true, null);

            return (false, "File does not appear to be a valid .xls file (invalid OLE2 or ZIP signature).");
        }

        private static (bool, string?) ValidateCsv(byte[] header, int bytesRead)
        {
            // CSV is plain text — ensure bytes are printable (ASCII / UTF-8 BOM allowed)
            // BOM: EF BB BF
            int start = (bytesRead >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF) ? 3 : 0;

            for (int i = start; i < bytesRead; i++)
            {
                var b = header[i];
                // Allow: printable ASCII, CR, LF, TAB
                bool printable = (b >= 0x20 && b <= 0x7E) || b == 0x0A || b == 0x0D || b == 0x09;
                // Allow: UTF-8 multi-byte lead bytes
                bool utf8Lead = b >= 0xC0;

                if (!printable && !utf8Lead)
                    return (false, "File does not appear to be a valid UTF-8 or ASCII CSV document (binary data detected).");
            }

            return (true, null);
        }

        private static string BuildBlobName(int clientId, string originalFileName)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var safeName = Path.GetFileNameWithoutExtension(originalFileName)
                               .Replace(" ", "_")
                               .Replace("..", "_");
            var ext = Path.GetExtension(originalFileName);
            return $"client-{clientId}/{timestamp}_{safeName}{ext}";
        }
    }
}
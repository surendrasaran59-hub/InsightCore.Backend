
namespace InsightCore.Shared.Helpers
{
    public static class UploadFileValidator
    {
        // ── Magic-byte signatures ───────────────────────────────────────────────
        // XLSX / XLS share the PK ZIP header (xlsx) or OLE2 header (xls)
        private static readonly byte[] ZipMagic = { 0x50, 0x4B, 0x03, 0x04 };   // PK.. (xlsx)
        private static readonly byte[] Ole2Magic = { 0xD0, 0xCF, 0x11, 0xE0 };   // OLE2 (xls)
        // CSV = plain text; we validate it has at least one comma or is printable ASCII/UTF-8
        public static async Task<(bool IsValid, string? ErrorMessage)> ValidateFileStreamAsync(
                Stream stream,
                string fileName) // CancellationToken cancellationToken
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            // Read first 8 bytes for magic-byte check
            var header = new byte[8];
            var bytesRead = await stream.ReadAsync(header, 0, header.Length);

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

        public static string BuildBlobName(int clientId, string clientName, string originalFileName)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var safeName = Path.GetFileNameWithoutExtension(originalFileName)
                               .Replace(" ", "_")
                               .Replace("..", "_");
            var ext = Path.GetExtension(originalFileName);
            return $"client-{clientId}-{clientName}/{timestamp}_{safeName}{ext}";
        }
    }

}

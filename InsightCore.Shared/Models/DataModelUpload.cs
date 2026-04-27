using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InsightCore.Shared.Models
{
    public class FileUploadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? BlobUrl { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class FileValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public string DetectedType { get; set; } = string.Empty;
    }

}

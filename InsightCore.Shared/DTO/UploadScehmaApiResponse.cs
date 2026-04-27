using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InsightCore.Shared.DTO
{
    public class UploadScehmaApiResponse
    {
        public string message { get; set; }
        public string blobUri { get; set; }
        public string blobName { get; set; }
        public int clientId { get; set; }
    }
}

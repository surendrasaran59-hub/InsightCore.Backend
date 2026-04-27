using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InsightCore.Shared.DTO
{
    public class ClientDataModel
    {
        public int ClientId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int CreatedBy { get; set; }
        public string UploadStatus { get; set; } 
        public string ProcessingStatus { get; set; } 
    }
}

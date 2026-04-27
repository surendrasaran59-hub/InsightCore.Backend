using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InsightCore.Shared.DTO
{
    public class ClientDto
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}

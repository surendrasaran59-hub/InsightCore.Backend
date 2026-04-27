using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InsightCore.Application.Interfaces
{
    public interface IEntitySchemaGenerator
    {
        string GenerateSchema(int clientId, int userId, string blobName);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InsightCore.Application
{
    public interface IEntitySchemaGenerator
    {
        string GenerateSchema(int clientId, int userId, string excelPath);
    }
}

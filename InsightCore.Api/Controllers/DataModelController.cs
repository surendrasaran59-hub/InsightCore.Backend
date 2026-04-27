using InsightCore.Application;
using InsightCore.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InsightCore.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DataModelController : ControllerBase
    {
        IEntitySchemaGenerator _schemaGenerator;

        public DataModelController(IEntitySchemaGenerator schemaGenerator)
        {
            _schemaGenerator = schemaGenerator;
        }

        [HttpGet]
        public async Task<IActionResult> Test()
        {
            int clientId = 1;
            int userId = 1;

            string blobName = "GEO_Data_Dictionary_V2.xlsx";
            string result = _schemaGenerator.GenerateSchema(clientId, userId, blobName);

            //if (!string.IsNullOrWhiteSpace(result))
            //    throw new ArgumentException(result);

            //result = string.IsNullOrWhiteSpace(result) ? "File processed successfully. Database schema has been created." : result;

            return Ok(result);
        }
    }
}
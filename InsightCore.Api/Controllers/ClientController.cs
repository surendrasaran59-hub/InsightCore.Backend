using InsightCore.Application.Interfaces;
using InsightCore.Shared.DTO;
using Microsoft.AspNetCore.Mvc;

namespace InsightCore.Api.Controllers
{
    /// <summary>
    /// Provides client reference data from Azure SQL via the Application layer.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ClientController : ControllerBase
    {
        private readonly IClientService _clientService;
        private readonly ILogger<ClientController> _logger;

        public ClientController(IClientService clientService, ILogger<ClientController> logger)
        {
            _clientService = clientService;
            _logger = logger;
        }

        // ── GET /api/client/getClients ──────────────────────────────────────────
        /// <summary>
        /// Returns all active clients from Azure SQL for populating the dropdown.
        /// </summary>
        [HttpGet("getClients")]
        [ProducesResponseType(typeof(IEnumerable<ClientDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetClients()
        {
            try
            {
                _logger.LogInformation("Fetching client list.");
                var clients = await _clientService.GetAllClientsAsync();
                return Ok(clients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching clients.");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while retrieving clients." });
            }
        }
    }
}

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

        //// ── GET /api/client/clientDataModel ──────────────────────────────────────────
        ///// <summary>
        ///// Returns all active clients from Azure SQL for populating the dropdown.
        ///// </summary>
        //[HttpPost("clientDataModel")]
        //[ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        //[ProducesResponseType(StatusCodes.Status500InternalServerError)]
        //public async Task<IActionResult> InsertClientDataModelAsync(ClientDataModel clientDataModel, CancellationToken cancellationToken = default)
        //{
        //    try
        //    {
        //        _logger.LogInformation("Inserting Client Data Model Information.");
        //        var clients = await _clientService.InsertClientDataAsync(clientDataModel, cancellationToken);
        //        return Ok(clients);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error inserting clients data model entry.");
        //        return StatusCode(StatusCodes.Status500InternalServerError,
        //            new { message = "An error occurred while inserting clients data model entry." });
        //    }
        //}

        // ── POST /api/client/clientDataModel ─────────────────────────────────────────
        /// <summary>
        /// Inserts a new ClientDataModel record and returns the generated identity ID.
        /// </summary>
        [HttpPost("clientDataModel")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> InsertClientDataModelAsync(
            [FromBody] ClientDataModel clientDataModel,          // ← added [FromBody]
            CancellationToken cancellationToken = default)
        {
            // ── ADDED: basic null guard ───────────────────────────────────────────
            if (clientDataModel is null)
                return BadRequest(new { message = "Request body cannot be null." });

            try
            {
                _logger.LogInformation("Inserting Client Data Model for ClientId: {ClientId}.",
                    clientDataModel.ClientId);

                var newId = await _clientService.InsertClientDataAsync(clientDataModel, cancellationToken);
                return Ok(newId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting client data model entry.");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while inserting client data model entry." });
            }
        }
    }
}

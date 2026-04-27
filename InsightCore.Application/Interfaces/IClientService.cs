using InsightCore.Shared.DTO;

namespace InsightCore.Application.Interfaces
{
    /// <summary>
    /// Provides client reference data from the Azure SQL database.
    /// </summary>
    public interface IClientService
    {
        /// <summary>Returns all active clients for the dropdown.</summary>
        Task<IEnumerable<ClientDto>> GetAllClientsAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns a single client by ID, or null if not found.</summary>
        Task<ClientDto?> GetClientByIdAsync(int clientId, CancellationToken cancellationToken = default);
    }
}
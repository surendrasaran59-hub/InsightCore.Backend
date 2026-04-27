using Dapper;
using InsightCore.Application.Interfaces;
using InsightCore.Shared.DTO;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InsightCore.Infrastructure.Implementations
{
    /// <summary>
    /// Retrieves client data from Azure SQL using Dapper.
    /// Connection string key: "ConnectionStrings:DefaultConnection"
    /// </summary>
    public class ClientService : IClientService
    {
        private readonly string _connectionString;
        private readonly ILogger<ClientService> _logger;

        public ClientService(IConfiguration configuration, ILogger<ClientService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not set.");
            _logger = logger;
        }

        public async Task<IEnumerable<ClientDto>> GetAllClientsAsync(
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT ClientId,
                       ClientName,
                       DisplayName,
                       IsActive
                FROM   MMP_Core.client
                WHERE  IsActive = 1
                ORDER  BY ClientName;
                """;

            _logger.LogInformation("Querying active clients from Azure SQL.");

            await using var connection = new SqlConnection(_connectionString);
            var clients = await connection.QueryAsync<ClientDto>(
                new CommandDefinition(sql, cancellationToken: cancellationToken));

            return clients;
        }

        public async Task<ClientDto?> GetClientByIdAsync(
            int clientId,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT ClientId,
                       ClientName,
                       DisplayName,
                       IsActive
                FROM   MMP_Core.client
                WHERE  ClientId  = @ClientId
                  AND  IsActive  = 1;
                """;

            await using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<ClientDto>(
                new CommandDefinition(sql, new { ClientId = clientId }, cancellationToken: cancellationToken));
        }
    }
}
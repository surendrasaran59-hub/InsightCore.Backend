using Dapper;
using InsightCore.Application.Interfaces;
using InsightCore.Shared.DTO;
using InsightCore.Shared.Helpers;
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
            _connectionString = configuration["insightcore-db-dev"] ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not set.");               
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

        public async Task<int> InsertClientDataAsync(
    ClientDataModel clientDataModel,
    CancellationToken cancellationToken = default)
        {
            const string sql = """
        INSERT INTO MMP_Core.ClientDataModel
               (ClientId,
                FileName,
                CreatedBy,
                UploadStatus,
                ProcessingStatus)
        VALUES (@ClientId,
                @FileName,
                @CreatedBy,
                @UploadStatus,
                @ProcessingStatus);
        SELECT CAST(SCOPE_IDENTITY() AS INT);
        """;

            var parameters = new DynamicParameters();
            parameters.Add("@ClientId", clientDataModel.ClientId);
            parameters.Add("@FileName", clientDataModel.FileName);
            parameters.Add("@CreatedBy", clientDataModel.CreatedBy);
            parameters.Add("@UploadStatus", clientDataModel.UploadStatus);
            parameters.Add("@ProcessingStatus", clientDataModel.ProcessingStatus);

            _logger.LogInformation(
                "Inserting client data record for ClientId: {ClientId}, File: {FileName}.",
                clientDataModel.ClientId,
                clientDataModel.FileName);

            await using var connection = new SqlConnection(_connectionString);

            var newId = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    sql,
                    parameters,
                    cancellationToken: cancellationToken));

            return newId;
        }
    }
}
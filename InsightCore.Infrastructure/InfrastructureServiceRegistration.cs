using InsightCore.Application.Interfaces;
using InsightCore.Infrastructure.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace InsightCore.Infrastructure
{
    /// <summary>
    /// Extension method to wire up all Infrastructure-layer services
    /// into the DI container from Program.cs / Startup.cs.
    /// </summary>
    public static class InfrastructureServiceRegistration
    {
        /// <summary>
        /// Call this in InsightCore.Api's Program.cs:
        /// <code>builder.Services.AddInfrastructureServices(builder.Configuration);</code>
        /// </summary>
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services)
        {
            // Azure Blob Storage
            services.AddSingleton<IBlobStorageService, BlobStorageService>();

            // Client data service (Azure SQL)
            services.AddScoped<IClientService, ClientService>();

            return services;
        }
    }
}
using AppTrace.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace AppTrace.Storage;

public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Add storage services with different performance tiers
    /// </summary>
    public static IServiceCollection AddAppTraceStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgreSQL") 
            ?? throw new InvalidOperationException("PostgreSQL connection string is required");

        var storageType = configuration.GetValue<string>("AppTrace:StorageType")?.ToLowerInvariant() ?? "standard";

        return storageType switch
        {
            "inmemory" => AddInMemoryStorage(services),
            "standard" => AddPostgreSqlStorage(services, connectionString),
            "bulk" => AddBulkStorage(services, connectionString),
            "highperformance" => AddBulkStorage(services, connectionString), // Alias
            _ => throw new ArgumentException($"Unknown storage type: {storageType}")
        };
    }

    private static IServiceCollection AddInMemoryStorage(IServiceCollection services)
    {
        services.AddSingleton<ILogStorage, InMemoryLogStorage>();
        services.AddSingleton<ITraceStorage, InMemoryTraceStorage>();
        services.AddSingleton<IMetricStorage, InMemoryMetricStorage>();
        return services;
    }

    private static IServiceCollection AddPostgreSqlStorage(IServiceCollection services, string connectionString)
    {
        services.AddScoped<ILogStorage>(provider => 
            new PostgreSqlLogStorage(connectionString, provider.GetRequiredService<ILogger<PostgreSqlLogStorage>>()));
        services.AddScoped<ITraceStorage>(provider => 
            new PostgreSqlTraceStorage(connectionString, provider.GetRequiredService<ILogger<PostgreSqlTraceStorage>>()));
        services.AddScoped<IMetricStorage>(provider => 
            new PostgreSqlMetricStorage(connectionString, provider.GetRequiredService<ILogger<PostgreSqlMetricStorage>>()));
        return services;
    }

    private static IServiceCollection AddBulkStorage(IServiceCollection services, string connectionString)
    {
        // Single implementation that handles all three interfaces
        services.AddScoped<PostgreSqlBulkStorage>(provider => 
            new PostgreSqlBulkStorage(connectionString, provider.GetRequiredService<ILogger<PostgreSqlBulkStorage>>()));
        
        services.AddScoped<ILogStorage>(provider => provider.GetRequiredService<PostgreSqlBulkStorage>());
        services.AddScoped<ITraceStorage>(provider => provider.GetRequiredService<PostgreSqlBulkStorage>());
        services.AddScoped<IMetricStorage>(provider => provider.GetRequiredService<PostgreSqlBulkStorage>());
        
        return services;
    }
}
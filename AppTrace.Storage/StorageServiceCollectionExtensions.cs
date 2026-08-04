using AppTrace.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

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

        var storageTypeValue = configuration.GetSection("AppTrace:StorageType").Value ?? nameof(StorageType.Standard);
        if (!Enum.TryParse<StorageType>(storageTypeValue, ignoreCase: true, out var storageType))
        {
            throw new ArgumentException($"Unknown storage type: {storageTypeValue}");
        }

        services.Configure<StoragePerformanceOptions>(configuration.GetSection(StoragePerformanceOptions.SectionName));
        services.AddSingleton<IngestionMetrics>();

        return storageType switch
        {
            StorageType.InMemory => AddInMemoryStorage(services),
            StorageType.Standard => AddPostgreSqlStorage(services, connectionString),
            StorageType.Bulk or StorageType.HighPerformance => AddBulkStorage(services, connectionString),
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
        services.AddSingleton(CreateDataSource(connectionString));

        services.AddSingleton<ILogStorage>(provider =>
            new PostgreSqlLogStorage(provider.GetRequiredService<NpgsqlDataSource>(), provider.GetRequiredService<ILogger<PostgreSqlLogStorage>>()));
        services.AddSingleton<ITraceStorage>(provider =>
            new PostgreSqlTraceStorage(provider.GetRequiredService<NpgsqlDataSource>(), provider.GetRequiredService<ILogger<PostgreSqlTraceStorage>>()));
        services.AddSingleton<IMetricStorage>(provider =>
            new PostgreSqlMetricStorage(provider.GetRequiredService<NpgsqlDataSource>(), provider.GetRequiredService<ILogger<PostgreSqlMetricStorage>>()));
        return services;
    }

    private static IServiceCollection AddBulkStorage(IServiceCollection services, string connectionString)
    {
        services.AddSingleton(CreateDataSource(connectionString));

        // Single implementation that handles all three interfaces
        services.AddSingleton<PostgreSqlBulkStorage>(provider =>
            new PostgreSqlBulkStorage(
                provider.GetRequiredService<NpgsqlDataSource>(),
                provider.GetRequiredService<ILogger<PostgreSqlBulkStorage>>(),
                provider.GetRequiredService<IOptions<StoragePerformanceOptions>>(),
                provider.GetRequiredService<IngestionMetrics>()));

        services.AddSingleton<ILogStorage>(provider => provider.GetRequiredService<PostgreSqlBulkStorage>());
        services.AddSingleton<ITraceStorage>(provider => provider.GetRequiredService<PostgreSqlBulkStorage>());
        services.AddSingleton<IMetricStorage>(provider => provider.GetRequiredService<PostgreSqlBulkStorage>());

        return services;
    }

    private static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        return dataSourceBuilder.Build();
    }
}
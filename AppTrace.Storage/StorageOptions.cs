namespace AppTrace.Storage;

/// <summary>
/// Selects which storage tier is wired up by <see cref="StorageServiceCollectionExtensions"/>.
/// </summary>
public enum StorageType
{
    Standard,
    InMemory,
    Bulk,

    /// <summary>Alias for <see cref="Bulk"/>.</summary>
    HighPerformance
}

/// <summary>
/// Strongly-typed performance tuning options for <see cref="PostgreSqlBulkStorage"/>,
/// bound once from the "AppTrace:Performance" configuration section.
/// </summary>
public sealed class StoragePerformanceOptions
{
    public const string SectionName = "AppTrace:Performance";

    public int BatchSize { get; set; } = 1000;
    public int ConnectionPoolSize { get; set; } = 4;
    public int MaxRetries { get; set; } = 3;
    public double RetryBackoffSeconds { get; set; } = 1;
}

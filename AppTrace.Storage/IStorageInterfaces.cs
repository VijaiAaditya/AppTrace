using AppTrace.Common.Models;

namespace AppTrace.Storage;

/// <summary>
/// A page of items alongside the total matching count, used to build
/// <see cref="TabularResult"/> responses without a separate count query
/// when the underlying store can report it in the same round-trip.
/// </summary>
public readonly record struct PagedResult<T>(IReadOnlyList<T> Items, long TotalCount);

public interface ILogStorage
{
    Task InsertLogsAsync(IEnumerable<LogEntry> logs);
    Task<IEnumerable<LogEntry>> GetLogsAsync(int limit = 100, int offset = 0);
    Task<IEnumerable<LogEntry>> SearchLogsAsync(string searchTerm, int limit = 100, int offset = 0);
    Task<PagedResult<LogEntry>> GetLogsPagedAsync(int page = 1, int pageSize = 100);
    Task<PagedResult<LogEntry>> SearchLogsPagedAsync(string searchTerm, int page = 1, int pageSize = 100);
}

public interface ITraceStorage
{
    Task InsertTracesAsync(IEnumerable<TraceEntry> traces);
    Task<IEnumerable<TraceEntry>> GetTracesAsync(int limit = 100, int offset = 0);
    Task<IEnumerable<TraceEntry>> GetTraceByIdAsync(string traceId);
    Task<PagedResult<TraceEntry>> GetTracesPagedAsync(int page = 1, int pageSize = 100);
}

public interface IMetricStorage
{
    Task InsertMetricsAsync(IEnumerable<MetricEntry> metrics);
    Task<IEnumerable<MetricEntry>> GetMetricsAsync(int limit = 100, int offset = 0);
    Task<IEnumerable<MetricEntry>> GetMetricsByNameAsync(string metricName, int limit = 100, int offset = 0);
    Task<PagedResult<MetricEntry>> GetMetricsPagedAsync(int page = 1, int pageSize = 100);
    Task<PagedResult<MetricEntry>> GetMetricsByNamePagedAsync(string metricName, int page = 1, int pageSize = 100);
}
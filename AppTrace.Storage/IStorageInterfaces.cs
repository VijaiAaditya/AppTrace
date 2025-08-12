using AppTrace.Common.Models;

namespace AppTrace.Storage;

public interface ILogStorage
{
    Task InsertLogsAsync(IEnumerable<LogEntry> logs);
    Task<IEnumerable<LogEntry>> GetLogsAsync(int limit = 100, int offset = 0);
    Task<IEnumerable<LogEntry>> SearchLogsAsync(string searchTerm, int limit = 100, int offset = 0);
}

public interface ITraceStorage
{
    Task InsertTracesAsync(IEnumerable<TraceEntry> traces);
    Task<IEnumerable<TraceEntry>> GetTracesAsync(int limit = 100, int offset = 0);
    Task<IEnumerable<TraceEntry>> GetTraceByIdAsync(string traceId);
}

public interface IMetricStorage
{
    Task InsertMetricsAsync(IEnumerable<MetricEntry> metrics);
    Task<IEnumerable<MetricEntry>> GetMetricsAsync(int limit = 100, int offset = 0);
    Task<IEnumerable<MetricEntry>> GetMetricsByNameAsync(string metricName, int limit = 100, int offset = 0);
}
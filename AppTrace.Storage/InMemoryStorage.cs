using AppTrace.Common.Models;
using System.Text.Json;

namespace AppTrace.Storage;

// In-memory storage implementation for demo purposes
// In production, you would implement this with Dapper + PostgreSQL
public class InMemoryLogStorage : ILogStorage
{
    private readonly List<LogEntry> _logs = new();
    private readonly object _lock = new();

    public Task InsertLogsAsync(IEnumerable<LogEntry> logs)
    {
        lock (_lock)
        {
            _logs.AddRange(logs);
        }
        return Task.CompletedTask;
    }

    public Task<IEnumerable<LogEntry>> GetLogsAsync(int limit = 100, int offset = 0)
    {
        lock (_lock)
        {
            return Task.FromResult(_logs
                .OrderByDescending(l => l.Timestamp)
                .Skip(offset)
                .Take(limit)
                .AsEnumerable());
        }
    }

    public Task<IEnumerable<LogEntry>> SearchLogsAsync(string searchTerm, int limit = 100, int offset = 0)
    {
        lock (_lock)
        {
            return Task.FromResult(_logs
                .Where(l => l.Body.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(l => l.Timestamp)
                .Skip(offset)
                .Take(limit)
                .AsEnumerable());
        }
    }
}

public class InMemoryTraceStorage : ITraceStorage
{
    private readonly List<TraceEntry> _traces = new();
    private readonly object _lock = new();

    public Task InsertTracesAsync(IEnumerable<TraceEntry> traces)
    {
        lock (_lock)
        {
            _traces.AddRange(traces);
        }
        return Task.CompletedTask;
    }

    public Task<IEnumerable<TraceEntry>> GetTracesAsync(int limit = 100, int offset = 0)
    {
        lock (_lock)
        {
            return Task.FromResult(_traces
                .OrderByDescending(t => t.StartTime)
                .Skip(offset)
                .Take(limit)
                .AsEnumerable());
        }
    }

    public Task<IEnumerable<TraceEntry>> GetTraceByIdAsync(string traceId)
    {
        lock (_lock)
        {
            return Task.FromResult(_traces
                .Where(t => t.TraceId == traceId)
                .OrderBy(t => t.StartTime)
                .AsEnumerable());
        }
    }
}

public class InMemoryMetricStorage : IMetricStorage
{
    private readonly List<MetricEntry> _metrics = new();
    private readonly object _lock = new();

    public Task InsertMetricsAsync(IEnumerable<MetricEntry> metrics)
    {
        lock (_lock)
        {
            _metrics.AddRange(metrics);
        }
        return Task.CompletedTask;
    }

    public Task<IEnumerable<MetricEntry>> GetMetricsAsync(int limit = 100, int offset = 0)
    {
        lock (_lock)
        {
            return Task.FromResult(_metrics
                .OrderByDescending(m => m.Timestamp)
                .Skip(offset)
                .Take(limit)
                .AsEnumerable());
        }
    }

    public Task<IEnumerable<MetricEntry>> GetMetricsByNameAsync(string metricName, int limit = 100, int offset = 0)
    {
        lock (_lock)
        {
            return Task.FromResult(_metrics
                .Where(m => m.Name.Contains(metricName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.Timestamp)
                .Skip(offset)
                .Take(limit)
                .AsEnumerable());
        }
    }
}
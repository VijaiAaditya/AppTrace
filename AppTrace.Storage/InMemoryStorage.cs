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

    public Task<PagedResult<LogEntry>> GetLogsPagedAsync(int page = 1, int pageSize = 100)
    {
        lock (_lock)
        {
            var ordered = _logs.OrderByDescending(l => l.Timestamp).ToList();
            var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedResult<LogEntry>(items, ordered.Count));
        }
    }

    public Task<PagedResult<LogEntry>> SearchLogsPagedAsync(string searchTerm, int page = 1, int pageSize = 100)
    {
        lock (_lock)
        {
            var filtered = _logs
                .Where(l => l.Body.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(l => l.Timestamp)
                .ToList();
            var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedResult<LogEntry>(items, filtered.Count));
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

    public Task<PagedResult<TraceEntry>> GetTracesPagedAsync(int page = 1, int pageSize = 100)
    {
        lock (_lock)
        {
            var ordered = _traces.OrderByDescending(t => t.StartTime).ToList();
            var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedResult<TraceEntry>(items, ordered.Count));
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

    public Task<PagedResult<MetricEntry>> GetMetricsPagedAsync(int page = 1, int pageSize = 100)
    {
        lock (_lock)
        {
            var ordered = _metrics.OrderByDescending(m => m.Timestamp).ToList();
            var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedResult<MetricEntry>(items, ordered.Count));
        }
    }

    public Task<PagedResult<MetricEntry>> GetMetricsByNamePagedAsync(string metricName, int page = 1, int pageSize = 100)
    {
        lock (_lock)
        {
            var filtered = _metrics
                .Where(m => m.Name.Contains(metricName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.Timestamp)
                .ToList();
            var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedResult<MetricEntry>(items, filtered.Count));
        }
    }
}
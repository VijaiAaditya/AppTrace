using AppTrace.Common.Models;
using Dapper;
using Npgsql;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AppTrace.Storage;

/// <summary>
/// PostgreSQL storage implementation using standard Dapper with bulk insert patterns
/// </summary>
public class PostgreSqlLogStorage : ILogStorage
{
    private readonly string _connectionString;
    private readonly ILogger<PostgreSqlLogStorage> _logger;

    public PostgreSqlLogStorage(string connectionString, ILogger<PostgreSqlLogStorage> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task InsertLogsAsync(IEnumerable<LogEntry> logs)
    {
        if (!logs.Any()) return;

        const string sql = @"
            INSERT INTO logs (id, timestamp, trace_id, span_id, severity, body, attributes, service_name)
            VALUES (@Id, @Timestamp, @TraceId, @SpanId, @Severity, @Body, @Attributes::jsonb, @ServiceName)";

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var parameters = logs.Select(log => new
            {
                Id = log.Id,
                Timestamp = log.Timestamp,
                TraceId = log.TraceId,
                SpanId = log.SpanId,
                Severity = log.Severity,
                Body = log.Body,
                Attributes = JsonSerializer.Serialize(log.Attributes ?? new Dictionary<string, object>()),
                ServiceName = log.Attributes?.GetValueOrDefault("service.name")?.ToString() ?? "unknown"
            });

            var affectedRows = await connection.ExecuteAsync(sql, parameters);
            _logger.LogDebug("Inserted {Count} log entries", affectedRows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert {Count} log entries", logs.Count());
            throw;
        }
    }

    public async Task<IEnumerable<LogEntry>> GetLogsAsync(int limit = 100, int offset = 0)
    {
        const string sql = @"
            SELECT id, timestamp, trace_id as TraceId, span_id as SpanId, severity, body, attributes, service_name
            FROM logs 
            ORDER BY timestamp DESC 
            LIMIT @Limit OFFSET @Offset";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var results = await connection.QueryAsync<dynamic>(sql, new { Limit = limit, Offset = offset });
        
        return results.Select(row => new LogEntry
        {
            Id = row.id,
            Timestamp = row.timestamp,
            TraceId = row.traceid ?? string.Empty,
            SpanId = row.spanid ?? string.Empty,
            Severity = row.severity ?? string.Empty,
            Body = row.body ?? string.Empty,
            Attributes = JsonSerializer.Deserialize<Dictionary<string, object>>(row.attributes?.ToString() ?? "{}")
        });
    }

    public async Task<IEnumerable<LogEntry>> SearchLogsAsync(string searchTerm, int limit = 100, int offset = 0)
    {
        const string sql = @"
            SELECT id, timestamp, trace_id as TraceId, span_id as SpanId, severity, body, attributes, service_name
            FROM logs 
            WHERE body ILIKE @SearchTerm OR attributes::text ILIKE @SearchTerm
            ORDER BY timestamp DESC 
            LIMIT @Limit OFFSET @Offset";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var searchPattern = $"%{searchTerm}%";
        var results = await connection.QueryAsync<dynamic>(sql, new { SearchTerm = searchPattern, Limit = limit, Offset = offset });
        
        return results.Select(row => new LogEntry
        {
            Id = row.id,
            Timestamp = row.timestamp,
            TraceId = row.traceid ?? string.Empty,
            SpanId = row.spanid ?? string.Empty,
            Severity = row.severity ?? string.Empty,
            Body = row.body ?? string.Empty,
            Attributes = JsonSerializer.Deserialize<Dictionary<string, object>>(row.attributes?.ToString() ?? "{}")
        });
    }
}

public class PostgreSqlTraceStorage : ITraceStorage
{
    private readonly string _connectionString;
    private readonly ILogger<PostgreSqlTraceStorage> _logger;

    public PostgreSqlTraceStorage(string connectionString, ILogger<PostgreSqlTraceStorage> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task InsertTracesAsync(IEnumerable<TraceEntry> traces)
    {
        if (!traces.Any()) return;

        const string sql = @"
            INSERT INTO traces (id, trace_id, span_id, parent_span_id, name, start_time, end_time, attributes, status, service_name, duration_ms)
            VALUES (@Id, @TraceId, @SpanId, @ParentSpanId, @Name, @StartTime, @EndTime, @Attributes::jsonb, @Status, @ServiceName, @DurationMs)";

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var parameters = traces.Select(trace => new
            {
                Id = trace.Id,
                TraceId = trace.TraceId,
                SpanId = trace.SpanId,
                ParentSpanId = trace.ParentSpanId,
                Name = trace.Name,
                StartTime = trace.StartTime,
                EndTime = trace.EndTime,
                Attributes = JsonSerializer.Serialize(trace.Attributes ?? new Dictionary<string, object>()),
                Status = trace.Status,
                ServiceName = trace.Attributes?.GetValueOrDefault("service.name")?.ToString() ?? "unknown",
                DurationMs = (trace.EndTime - trace.StartTime).TotalMilliseconds
            });

            var affectedRows = await connection.ExecuteAsync(sql, parameters);
            _logger.LogDebug("Inserted {Count} trace entries", affectedRows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert {Count} trace entries", traces.Count());
            throw;
        }
    }

    public async Task<IEnumerable<TraceEntry>> GetTracesAsync(int limit = 100, int offset = 0)
    {
        const string sql = @"
            SELECT id, trace_id as TraceId, span_id as SpanId, parent_span_id as ParentSpanId, 
                   name, start_time as StartTime, end_time as EndTime, attributes, status
            FROM traces 
            ORDER BY start_time DESC 
            LIMIT @Limit OFFSET @Offset";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var results = await connection.QueryAsync<dynamic>(sql, new { Limit = limit, Offset = offset });
        
        return results.Select(row => new TraceEntry
        {
            Id = row.id,
            TraceId = row.traceid ?? string.Empty,
            SpanId = row.spanid ?? string.Empty,
            ParentSpanId = row.parentspanid ?? string.Empty,
            Name = row.name ?? string.Empty,
            StartTime = row.starttime,
            EndTime = row.endtime,
            Attributes = JsonSerializer.Deserialize<Dictionary<string, object>>(row.attributes?.ToString() ?? "{}"),
            Status = row.status ?? string.Empty
        });
    }

    public async Task<IEnumerable<TraceEntry>> GetTraceByIdAsync(string traceId)
    {
        const string sql = @"
            SELECT id, trace_id as TraceId, span_id as SpanId, parent_span_id as ParentSpanId, 
                   name, start_time as StartTime, end_time as EndTime, attributes, status
            FROM traces 
            WHERE trace_id = @TraceId
            ORDER BY start_time ASC";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var results = await connection.QueryAsync<dynamic>(sql, new { TraceId = traceId });
        
        return results.Select(row => new TraceEntry
        {
            Id = row.id,
            TraceId = row.traceid ?? string.Empty,
            SpanId = row.spanid ?? string.Empty,
            ParentSpanId = row.parentspanid ?? string.Empty,
            Name = row.name ?? string.Empty,
            StartTime = row.starttime,
            EndTime = row.endtime,
            Attributes = JsonSerializer.Deserialize<Dictionary<string, object>>(row.attributes?.ToString() ?? "{}"),
            Status = row.status ?? string.Empty
        });
    }
}

public class PostgreSqlMetricStorage : IMetricStorage
{
    private readonly string _connectionString;
    private readonly ILogger<PostgreSqlMetricStorage> _logger;

    public PostgreSqlMetricStorage(string connectionString, ILogger<PostgreSqlMetricStorage> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task InsertMetricsAsync(IEnumerable<MetricEntry> metrics)
    {
        if (!metrics.Any()) return;

        const string sql = @"
            INSERT INTO metrics (id, name, timestamp, value, attributes, service_name)
            VALUES (@Id, @Name, @Timestamp, @Value, @Attributes::jsonb, @ServiceName)";

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var parameters = metrics.Select(metric => new
            {
                Id = metric.Id,
                Name = metric.Name,
                Timestamp = metric.Timestamp,
                Value = metric.Value,
                Attributes = JsonSerializer.Serialize(metric.Attributes ?? new Dictionary<string, object>()),
                ServiceName = metric.Attributes?.GetValueOrDefault("service.name")?.ToString() ?? "unknown"
            });

            var affectedRows = await connection.ExecuteAsync(sql, parameters);
            _logger.LogDebug("Inserted {Count} metric entries", affectedRows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert {Count} metric entries", metrics.Count());
            throw;
        }
    }

    public async Task<IEnumerable<MetricEntry>> GetMetricsAsync(int limit = 100, int offset = 0)
    {
        const string sql = @"
            SELECT id, name, timestamp, value, attributes
            FROM metrics 
            ORDER BY timestamp DESC 
            LIMIT @Limit OFFSET @Offset";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var results = await connection.QueryAsync<dynamic>(sql, new { Limit = limit, Offset = offset });
        
        return results.Select(row => new MetricEntry
        {
            Id = row.id,
            Name = row.name ?? string.Empty,
            Timestamp = row.timestamp,
            Value = row.value,
            Attributes = JsonSerializer.Deserialize<Dictionary<string, object>>(row.attributes?.ToString() ?? "{}")
        });
    }

    public async Task<IEnumerable<MetricEntry>> GetMetricsByNameAsync(string metricName, int limit = 100, int offset = 0)
    {
        const string sql = @"
            SELECT id, name, timestamp, value, attributes
            FROM metrics 
            WHERE name ILIKE @MetricName
            ORDER BY timestamp DESC 
            LIMIT @Limit OFFSET @Offset";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var searchPattern = $"%{metricName}%";
        var results = await connection.QueryAsync<dynamic>(sql, new { MetricName = searchPattern, Limit = limit, Offset = offset });
        
        return results.Select(row => new MetricEntry
        {
            Id = row.id,
            Name = row.name ?? string.Empty,
            Timestamp = row.timestamp,
            Value = row.value,
            Attributes = JsonSerializer.Deserialize<Dictionary<string, object>>(row.attributes?.ToString() ?? "{}")
        });
    }
}
using AppTrace.Common.Models;
using Dapper;
using Npgsql;
using System.Text.Json;
using System.Data;
using Microsoft.Extensions.Logging;
using NpgsqlTypes;

namespace AppTrace.Storage;

/// <summary>
/// High-performance PostgreSQL storage using PostgreSQL COPY for ultra-fast bulk insertions
/// </summary>
public class PostgreSqlBulkStorage : ILogStorage, ITraceStorage, IMetricStorage
{
    private readonly string _connectionString;
    private readonly ILogger<PostgreSqlBulkStorage> _logger;

    public PostgreSqlBulkStorage(string connectionString, ILogger<PostgreSqlBulkStorage> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    // ============ LOG STORAGE ============
    public async Task InsertLogsAsync(IEnumerable<LogEntry> logs)
    {
        if (!logs.Any()) return;

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Use PostgreSQL COPY for maximum performance
            using var writer = await connection.BeginBinaryImportAsync(
                "COPY logs (id, timestamp, trace_id, span_id, severity, body, attributes, service_name) FROM STDIN (FORMAT BINARY)");

            foreach (var log in logs)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(log.Id, NpgsqlDbType.Uuid);
                await writer.WriteAsync(log.Timestamp, NpgsqlDbType.TimestampTz);
                await writer.WriteAsync(log.TraceId ?? (object)DBNull.Value, NpgsqlDbType.Text);
                await writer.WriteAsync(log.SpanId ?? (object)DBNull.Value, NpgsqlDbType.Text);
                await writer.WriteAsync(log.Severity ?? (object)DBNull.Value, NpgsqlDbType.Text);
                await writer.WriteAsync(log.Body ?? (object)DBNull.Value, NpgsqlDbType.Text);
                await writer.WriteAsync(JsonSerializer.Serialize(log.Attributes ?? new Dictionary<string, object>()), NpgsqlDbType.Jsonb);
                await writer.WriteAsync(log.Attributes?.GetValueOrDefault("service.name")?.ToString() ?? "unknown", NpgsqlDbType.Text);
            }

            await writer.CompleteAsync();
            _logger.LogDebug("Bulk inserted {Count} log entries using COPY", logs.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk insert {Count} log entries", logs.Count());
            // Fallback to standard insert
            await FallbackInsertLogsAsync(logs);
        }
    }

    private async Task FallbackInsertLogsAsync(IEnumerable<LogEntry> logs)
    {
        const string sql = @"
            INSERT INTO logs (id, timestamp, trace_id, span_id, severity, body, attributes, service_name)
            VALUES (@Id, @Timestamp, @TraceId, @SpanId, @Severity, @Body, @Attributes::jsonb, @ServiceName)";

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

        await connection.ExecuteAsync(sql, parameters);
        _logger.LogDebug("Fallback inserted {Count} log entries", logs.Count());
    }

    public async Task<IEnumerable<LogEntry>> GetLogsAsync(int limit = 100, int offset = 0)
    {
        const string sql = @"
            SELECT id, timestamp, trace_id as TraceId, span_id as SpanId, severity, body, attributes
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
            SELECT id, timestamp, trace_id as TraceId, span_id as SpanId, severity, body, attributes
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

    // ============ TRACE STORAGE ============
    public async Task InsertTracesAsync(IEnumerable<TraceEntry> traces)
    {
        if (!traces.Any()) return;

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var writer = await connection.BeginBinaryImportAsync(
                "COPY traces (id, trace_id, span_id, parent_span_id, name, start_time, end_time, attributes, status, service_name) FROM STDIN (FORMAT BINARY)");

            foreach (var trace in traces)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(trace.Id, NpgsqlDbType.Uuid);
                await writer.WriteAsync(trace.TraceId ?? (object)DBNull.Value, NpgsqlDbType.Text);
                await writer.WriteAsync(trace.SpanId ?? (object)DBNull.Value, NpgsqlDbType.Text);
                await writer.WriteAsync(trace.ParentSpanId ?? (object)DBNull.Value, NpgsqlDbType.Text);
                await writer.WriteAsync(trace.Name ?? (object)DBNull.Value, NpgsqlDbType.Text);
                await writer.WriteAsync(trace.StartTime, NpgsqlDbType.TimestampTz);
                await writer.WriteAsync(trace.EndTime, NpgsqlDbType.TimestampTz);
                await writer.WriteAsync(JsonSerializer.Serialize(trace.Attributes ?? new Dictionary<string, object>()), NpgsqlDbType.Jsonb);
                await writer.WriteAsync(trace.Status ?? "OK", NpgsqlDbType.Text);
                await writer.WriteAsync(trace.Attributes?.GetValueOrDefault("service.name")?.ToString() ?? "unknown", NpgsqlDbType.Text);
            }

            await writer.CompleteAsync();
            _logger.LogDebug("Bulk inserted {Count} trace entries using COPY", traces.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk insert {Count} trace entries", traces.Count());
            await FallbackInsertTracesAsync(traces);
        }
    }

    private async Task FallbackInsertTracesAsync(IEnumerable<TraceEntry> traces)
    {
        const string sql = @"
            INSERT INTO traces (id, trace_id, span_id, parent_span_id, name, start_time, end_time, attributes, status, service_name)
            VALUES (@Id, @TraceId, @SpanId, @ParentSpanId, @Name, @StartTime, @EndTime, @Attributes::jsonb, @Status, @ServiceName)";

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
            ServiceName = trace.Attributes?.GetValueOrDefault("service.name")?.ToString() ?? "unknown"
        });

        await connection.ExecuteAsync(sql, parameters);
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

    // ============ METRIC STORAGE ============
    public async Task InsertMetricsAsync(IEnumerable<MetricEntry> metrics)
    {
        if (!metrics.Any()) return;

        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var writer = await connection.BeginBinaryImportAsync(
                "COPY metrics (id, name, timestamp, value, attributes, service_name) FROM STDIN (FORMAT BINARY)");

            foreach (var metric in metrics)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(metric.Id, NpgsqlDbType.Uuid);
                await writer.WriteAsync(metric.Name ?? (object)DBNull.Value, NpgsqlDbType.Text);
                await writer.WriteAsync(metric.Timestamp, NpgsqlDbType.TimestampTz);
                await writer.WriteAsync(metric.Value, NpgsqlDbType.Double);
                await writer.WriteAsync(JsonSerializer.Serialize(metric.Attributes ?? new Dictionary<string, object>()), NpgsqlDbType.Jsonb);
                await writer.WriteAsync(metric.Attributes?.GetValueOrDefault("service.name")?.ToString() ?? "unknown", NpgsqlDbType.Text);
            }

            await writer.CompleteAsync();
            _logger.LogDebug("Bulk inserted {Count} metric entries using COPY", metrics.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk insert {Count} metric entries", metrics.Count());
            await FallbackInsertMetricsAsync(metrics);
        }
    }

    private async Task FallbackInsertMetricsAsync(IEnumerable<MetricEntry> metrics)
    {
        const string sql = @"
            INSERT INTO metrics (id, name, timestamp, value, attributes, service_name)
            VALUES (@Id, @Name, @Timestamp, @Value, @Attributes::jsonb, @ServiceName)";

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

        await connection.ExecuteAsync(sql, parameters);
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
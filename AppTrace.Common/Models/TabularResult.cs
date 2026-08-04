using System.Text.Json;

namespace AppTrace.Common.Models;

/// <summary>
/// A "DataTable-like" query result shape: a fixed column list plus rows of
/// values in column order. This avoids repeating property/attribute key
/// names per record (as plain JSON arrays-of-objects would), which keeps
/// payloads small and lets clients bind to a grid without reflection -
/// critical when paging through tens/hundreds of thousands of records.
/// </summary>
public sealed class TabularResult
{
    public string[] Columns { get; init; } = [];
    public object?[][] Rows { get; init; } = [];
    public long TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

/// <summary>
/// Maps storage entity collections into <see cref="TabularResult"/> shapes.
/// Column order/names are fixed per entity type so client-side renderers can
/// format cells (e.g., timestamps) by known column index without guessing.
/// </summary>
public static class TabularResultExtensions
{
    public static readonly string[] LogColumns =
        ["Id", "Timestamp", "Severity", "ServiceName", "TraceId", "SpanId", "Body", "Attributes"];

    public static readonly string[] TraceColumns =
        ["Id", "TraceId", "SpanId", "ParentSpanId", "Name", "StartTime", "EndTime", "DurationMs", "Status", "Attributes"];

    public static readonly string[] MetricColumns =
        ["Id", "Name", "Timestamp", "Value", "Attributes"];

    public static TabularResult ToTabularResult(this IReadOnlyList<LogEntry> logs, long totalCount, int page, int pageSize)
    {
        var rows = new object?[logs.Count][];
        for (var i = 0; i < logs.Count; i++)
        {
            var log = logs[i];
            rows[i] =
            [
                log.Id,
                log.Timestamp,
                log.Severity,
                log.Attributes?.GetValueOrDefault("service.name")?.ToString() ?? "unknown",
                log.TraceId,
                log.SpanId,
                log.Body,
                SerializeAttributes(log.Attributes)
            ];
        }

        return new TabularResult { Columns = LogColumns, Rows = rows, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }

    public static TabularResult ToTabularResult(this IReadOnlyList<TraceEntry> traces, long totalCount, int page, int pageSize)
    {
        var rows = new object?[traces.Count][];
        for (var i = 0; i < traces.Count; i++)
        {
            var trace = traces[i];
            rows[i] =
            [
                trace.Id,
                trace.TraceId,
                trace.SpanId,
                trace.ParentSpanId,
                trace.Name,
                trace.StartTime,
                trace.EndTime,
                (trace.EndTime - trace.StartTime).TotalMilliseconds,
                trace.Status,
                SerializeAttributes(trace.Attributes)
            ];
        }

        return new TabularResult { Columns = TraceColumns, Rows = rows, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }

    public static TabularResult ToTabularResult(this IReadOnlyList<MetricEntry> metrics, long totalCount, int page, int pageSize)
    {
        var rows = new object?[metrics.Count][];
        for (var i = 0; i < metrics.Count; i++)
        {
            var metric = metrics[i];
            rows[i] =
            [
                metric.Id,
                metric.Name,
                metric.Timestamp,
                metric.Value,
                SerializeAttributes(metric.Attributes)
            ];
        }

        return new TabularResult { Columns = MetricColumns, Rows = rows, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }

    private static string SerializeAttributes(Dictionary<string, object>? attributes)
    {
        if (attributes is null || attributes.Count == 0) return string.Empty;
        return JsonSerializer.Serialize(attributes);
    }
}

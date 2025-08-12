namespace AppTrace.Common.Models;

public class LogEntry
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string SpanId { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Dictionary<string, object>? Attributes { get; set; }
}

public class TraceEntry
{
    public Guid Id { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string SpanId { get; set; } = string.Empty;
    public string ParentSpanId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public Dictionary<string, object>? Attributes { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class MetricEntry
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public double Value { get; set; }
    public Dictionary<string, object>? Attributes { get; set; }
}

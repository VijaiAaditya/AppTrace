using AppTrace.Common.Models;
using AppTrace.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Enable HTTP/3 (with automatic fallback to HTTP/2 / HTTP/1.1 negotiated via Alt-Svc) for this
// external-facing Query API. HTTP/3 requires TLS, so it only applies to the https endpoint;
// http endpoints continue to negotiate HTTP/1.1 or HTTP/2 as before.
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
    });
});

builder.Services.AddOpenApi();
builder.Services.AddAppTraceStorage(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

// Advertise HTTP/3 availability to clients connecting over HTTP/1.1 or HTTP/2 so they can
// upgrade on subsequent requests (QUIC/UDP on the same port as the HTTPS endpoint).
app.Use(async (context, next) =>
{
    context.Response.Headers.AltSvc = "h3=\":443\"; ma=86400";
    await next();
});

const int MaxPageSize = 100_000;

static int NormalizePage(int page) => page < 1 ? 1 : page;
static int NormalizePageSize(int pageSize) => pageSize <= 0 ? 100 : Math.Min(pageSize, MaxPageSize);

// ============ LOGS ============
app.MapGet("/api/logs", async (ILogStorage storage, int page = 1, int pageSize = 100) =>
{
    var result = await storage.GetLogsPagedAsync(NormalizePage(page), NormalizePageSize(pageSize));
    return Results.Ok(result.Items.ToTabularResult(result.TotalCount, NormalizePage(page), NormalizePageSize(pageSize)));
})
.WithName("GetLogs");

app.MapGet("/api/logs/search", async (ILogStorage storage, [FromQuery] string term, int page = 1, int pageSize = 100) =>
{
    var result = await storage.SearchLogsPagedAsync(term, NormalizePage(page), NormalizePageSize(pageSize));
    return Results.Ok(result.Items.ToTabularResult(result.TotalCount, NormalizePage(page), NormalizePageSize(pageSize)));
})
.WithName("SearchLogs");

// ============ TRACES ============
app.MapGet("/api/traces", async (ITraceStorage storage, int page = 1, int pageSize = 100) =>
{
    var result = await storage.GetTracesPagedAsync(NormalizePage(page), NormalizePageSize(pageSize));
    return Results.Ok(result.Items.ToTabularResult(result.TotalCount, NormalizePage(page), NormalizePageSize(pageSize)));
})
.WithName("GetTraces");

app.MapGet("/api/traces/{traceId}", async (ITraceStorage storage, string traceId) =>
{
    var spans = (await storage.GetTraceByIdAsync(traceId)).ToList();
    return Results.Ok(spans.ToTabularResult(spans.Count, 1, spans.Count == 0 ? 100 : spans.Count));
})
.WithName("GetTraceById");

// ============ METRICS ============
app.MapGet("/api/metrics", async (IMetricStorage storage, int page = 1, int pageSize = 100) =>
{
    var result = await storage.GetMetricsPagedAsync(NormalizePage(page), NormalizePageSize(pageSize));
    return Results.Ok(result.Items.ToTabularResult(result.TotalCount, NormalizePage(page), NormalizePageSize(pageSize)));
})
.WithName("GetMetrics");

app.MapGet("/api/metrics/{name}", async (IMetricStorage storage, string name, int page = 1, int pageSize = 100) =>
{
    var result = await storage.GetMetricsByNamePagedAsync(name, NormalizePage(page), NormalizePageSize(pageSize));
    return Results.Ok(result.Items.ToTabularResult(result.TotalCount, NormalizePage(page), NormalizePageSize(pageSize)));
})
.WithName("GetMetricsByName");

// ============ EXPORT (CSV) ============
app.MapGet("/api/export/{type}", async (string type, ILogStorage logStorage, ITraceStorage traceStorage, IMetricStorage metricStorage, int page = 1, int pageSize = 1000) =>
{
    TabularResult tabular = type.ToLowerInvariant() switch
    {
        "logs" => (await logStorage.GetLogsPagedAsync(NormalizePage(page), NormalizePageSize(pageSize))) is var r1
            ? r1.Items.ToTabularResult(r1.TotalCount, NormalizePage(page), NormalizePageSize(pageSize))
            : throw new InvalidOperationException(),
        "traces" => (await traceStorage.GetTracesPagedAsync(NormalizePage(page), NormalizePageSize(pageSize))) is var r2
            ? r2.Items.ToTabularResult(r2.TotalCount, NormalizePage(page), NormalizePageSize(pageSize))
            : throw new InvalidOperationException(),
        "metrics" => (await metricStorage.GetMetricsPagedAsync(NormalizePage(page), NormalizePageSize(pageSize))) is var r3
            ? r3.Items.ToTabularResult(r3.TotalCount, NormalizePage(page), NormalizePageSize(pageSize))
            : throw new InvalidOperationException(),
        _ => throw new ArgumentException($"Unknown export type: {type}")
    };

    var csv = ToCsv(tabular);
    return Results.Text(csv, "text/csv");
})
.WithName("ExportCsv");

app.MapGet("/health", () => new { Status = "Healthy", Timestamp = DateTimeOffset.UtcNow });
app.MapGet("/metrics/query", (IngestionMetrics metrics) => metrics.GetSnapshot());

app.Run();

static string ToCsv(TabularResult tabular)
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine(string.Join(',', tabular.Columns));
    foreach (var row in tabular.Rows)
    {
        sb.AppendLine(string.Join(',', row.Select(v => EscapeCsv(v?.ToString() ?? string.Empty))));
    }
    return sb.ToString();
}

static string EscapeCsv(string value)
{
    if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
    return value;
}


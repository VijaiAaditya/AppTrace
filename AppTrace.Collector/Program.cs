using AppTrace.Collector.Services;
using AppTrace.Storage;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTP/2 (gRPC)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });

    // Optional: increase limits for large OTLP payloads
    options.Limits.MaxRequestBodySize = 1024 * 1024 * 50; // 50 MB
});

// Add gRPC
builder.Services.AddGrpc();

// Add storage services - configurable via appsettings.json
builder.Services.AddAppTraceStorage(builder.Configuration);

// Add CORS for web UI
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors();

// Map gRPC services
app.MapGrpcService<OtlpTraceService>();
app.MapGrpcService<OtlpLogsService>();
app.MapGrpcService<OtlpMetricsService>();

// Health check endpoint
app.MapGet("/", () => "AppTrace Collector is running. gRPC services available on port 4317.");
app.MapGet("/health", () => new { Status = "Healthy", Timestamp = DateTimeOffset.UtcNow });

// HTTP/2 endpoint for gRPC
app.Urls.Add("http://localhost:4317");

app.Run();

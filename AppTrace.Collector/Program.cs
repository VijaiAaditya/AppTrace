using AppTrace.Collector.Services;
using AppTrace.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add gRPC
builder.Services.AddGrpc();

// Add storage services - configurable via appsettings.json
builder.Services.AddAppTraceStorage(builder.Configuration);

// Add CORS for web UI
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.WithOrigins("*")
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

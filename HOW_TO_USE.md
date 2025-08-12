# ??? How to Use AppTrace from Any Application

## Overview

Your AppTrace collector is now ready to receive OpenTelemetry data! Here's how any .NET application can send logs, traces, and metrics to your AppTrace collector automatically.

## ? What the Collector Does

Your AppTrace collector now:

1. **? Accepts OTLP (OpenTelemetry Protocol) data** via gRPC on port 4317
2. **? Processes traces, logs, and metrics** automatically  
3. **? Stores data in memory** (ready to be replaced with PostgreSQL + Dapper)
4. **? Provides gRPC services** compatible with standard OpenTelemetry exporters

---

## ?? How to Send Data from Any Application

### Step 1: Install OpenTelemetry Packages

In any .NET application, install these NuGet packages:

```bash
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http  
dotnet add package OpenTelemetry.Instrumentation.SqlClient
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

### Step 2: Configure OpenTelemetry in Program.cs

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("MyDemoApp", serviceVersion: "1.0.0")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = "development",
            ["service.instance.id"] = Environment.MachineName
        }))
    .WithTracing(tracerProviderBuilder => tracerProviderBuilder
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317"); // Your AppTrace Collector
            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        }))
    .WithMetrics(meterProviderBuilder => meterProviderBuilder
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317");
            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        }));

// Configure logging to also send to AppTrace
builder.Logging.AddOpenTelemetry(options =>
{
    options.AddOtlpExporter(otlpOptions =>
    {
        otlpOptions.Endpoint = new Uri("http://localhost:4317");
        otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseRouting();
app.MapControllers();

app.Run();
```

### Step 3: That's It! ??

With this configuration, your application will **automatically** send:

- **?? HTTP Request traces** (incoming and outgoing)
- **??? Database call traces** (if using SqlClient)  
- **?? Runtime metrics** (GC, memory, CPU)
- **?? Application logs** (via ILogger)

**No additional code needed!** This works exactly like Azure Application Insights.

---

## ?? Test Your Setup

### Create a Simple Test Controller

```csharp
[ApiController]
[Route("[controller]")]
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;
    private readonly HttpClient _httpClient;

    public TestController(ILogger<TestController> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    [HttpGet("hello")]
    public async Task<IActionResult> Hello()
    {
        _logger.LogInformation("Hello endpoint called at {Time}", DateTime.UtcNow);
        
        // This will create a trace for the HTTP call
        var response = await _httpClient.GetStringAsync("https://api.github.com/");
        
        _logger.LogInformation("GitHub API responded with {Length} characters", response.Length);
        
        return Ok(new { Message = "Hello from traced application!", GitHubApiLength = response.Length });
    }

    [HttpGet("error")]
    public IActionResult SimulateError()
    {
        _logger.LogError("Simulating an error for testing");
        throw new InvalidOperationException("This is a test error for AppTrace");
    }
}
```

### Run Your App and Test

1. **Start your AppTrace Collector**: 
   ```bash
   cd AppTrace.Collector
   dotnet run
   ```

2. **Start your test application**:
   ```bash
   dotnet run
   ```

3. **Make some requests**:
   ```bash
   curl http://localhost:5000/test/hello
   curl http://localhost:5000/test/error
   ```

4. **Check the AppTrace Collector logs** - you should see:
   ```
   info: AppTrace.Collector.Services.OtlpTraceService[0]
         Processed 3 trace spans
   info: AppTrace.Collector.Services.OtlpLogsService[0]
         Processed 2 log records
   info: AppTrace.Collector.Services.OtlpMetricsService[0]
         Processed 15 metric data points
   ```

---

## ?? What Data Gets Collected Automatically

| Type | What's Captured | Source |
|------|----------------|---------|
| **Traces** | HTTP requests, HTTP client calls, SQL queries | OpenTelemetry Auto-Instrumentation |
| **Logs** | All ILogger calls with context | OpenTelemetry Logging Provider |
| **Metrics** | Request duration, request count, GC metrics, memory usage | Runtime + ASP.NET Core Instrumentation |

---

## ?? Advanced Configuration

### Custom Attributes per Service

```csharp
.ConfigureResource(resource => resource
    .AddService("UserService", serviceVersion: "2.1.0")
    .AddAttributes(new Dictionary<string, object>
    {
        ["team"] = "platform",
        ["cost.center"] = "engineering",
        ["deployment.environment"] = builder.Environment.EnvironmentName
    }))
```

### Custom Metrics

```csharp
public class OrderController : ControllerBase
{
    private static readonly Counter<int> OrdersProcessed = 
        Meter.CreateCounter<int>("orders.processed");

    [HttpPost]
    public IActionResult CreateOrder()
    {
        // Your business logic here
        
        OrdersProcessed.Add(1, new("status", "created"));
        return Ok();
    }
}
```

---

## ?? Next Steps

1. **? Data Collection** - Working! Your collector receives OTLP data
2. **?? Replace In-Memory Storage** - Implement PostgreSQL + Dapper storage 
3. **??? Build Query UI** - Create Blazor WebAssembly frontend
4. **?? Add Dashboards** - Visualize metrics and traces
5. **?? Add Search** - Search logs and traces
6. **?? Add Export** - CSV/JSON export functionality

Your AppTrace tool now works like Azure Application Insights but **completely local and self-hosted**! ??
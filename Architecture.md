## ✅ Project Summary

> A lightweight, local-first observability tool built in .NET 9 using Blazor WebAssembly (UI), gRPC (telemetry ingestion), Dapper (data access), and PostgreSQL (storage). It supports OpenTelemetry-compatible logs, traces, and metrics, with query APIs and a rich UI.

---

## 📁 Folder & Solution Structure

```
TelemetryTool.sln
│
├── src/
│   ├── Telemetry.Api/                 // gRPC Service for intake
│   ├── Telemetry.Storage/            // Dapper + PGSQL abstractions
│   ├── Telemetry.Query/              // Query APIs for logs, traces, metrics
│   ├── Telemetry.UI/                 // Blazor WASM App (UI)
│   └── Telemetry.Shared/             // Common models, DTOs, enums
│
├── database/
│   ├── migrations.sql                // Initial schema
│   └── seed.sql                      // Optional test data
│
└── docs/
    └── README.md
```

---

## 🧱 Tech Stack & Tools

| Layer           | Choice                                    | Notes                         |
| --------------- | ----------------------------------------- | ----------------------------- |
| Frontend UI     | Blazor WebAssembly                        | Embedded in `Telemetry.UI`    |
| API Intake      | gRPC (.proto)                             | Hosted in `Telemetry.Api`     |
| DB Access       | Dapper                                    | Fast, lightweight SQL mapping |
| DB Engine       | PostgreSQL                                | Structured + JSONB support    |
| Query API       | ASP.NET Core Minimal APIs or gRPC         | In `Telemetry.Query`          |
| Models          | OpenTelemetry schema (log, trace, metric) | In `Telemetry.Shared`         |
| Hosting         | Console app or ASP.NET Core hosted        | Self-contained                |
| Auth (optional) | Azure AAD + Easy Auth or static token     | External if needed            |
| Exporting       | JSON/CSV download from UI                 | Client-driven via Blazor      |

---

## 📦 gRPC Services to Define (OTLP-Compatible)

Define gRPC services that map to:

* `ExportLogsServiceRequest`
* `ExportMetricsServiceRequest`
* `ExportTraceServiceRequest`

This means:

* Accept OTLP proto (or your simplified version)
* Transform incoming data and write to PostgreSQL via `Telemetry.Storage`

---

## 📇 PostgreSQL Schema Design

### Logs Table

```sql
CREATE TABLE Logs (
  Id UUID PRIMARY KEY,
  Timestamp TIMESTAMPTZ,
  Severity TEXT,
  ServiceName TEXT,
  Message TEXT,
  Attributes JSONB
);
```

### Traces Table

```sql
CREATE TABLE Traces (
  SpanId UUID PRIMARY KEY,
  TraceId UUID,
  ParentSpanId UUID,
  ServiceName TEXT,
  SpanName TEXT,
  StartTime TIMESTAMPTZ,
  DurationMs DOUBLE PRECISION,
  Attributes JSONB
);
```

### Metrics Table

```sql
CREATE TABLE Metrics (
  Id UUID PRIMARY KEY,
  Timestamp TIMESTAMPTZ,
  MetricName TEXT,
  ServiceName TEXT,
  Value DOUBLE PRECISION,
  Labels JSONB
);
```

✅ Add indexes:

```sql
CREATE INDEX idx_logs_timestamp ON Logs(Timestamp);
CREATE INDEX idx_traces_traceid ON Traces(TraceId);
CREATE INDEX idx_metrics_timestamp ON Metrics(Timestamp);
CREATE INDEX idx_logs_attributes_jsonb ON Logs USING GIN (Attributes);
```

---

## 🧑‍💻 Implementation Tasks (High-Level)

### 1. Telemetry.Api

* gRPC server
* Protobuf import (OTLP or simplified)
* Map proto to models
* Call `Telemetry.Storage` to insert

### 2. Telemetry.Storage

* Dapper DB context
* SQL insert/query commands
* Connection pooling
* Resilience (retry on fail)

### 3. Telemetry.Query

* Expose `/api/logs`, `/api/trace/{id}`, `/api/metrics` (or via gRPC)
* Parameter filtering
* Pagination, aggregation (if needed)

### 4. Telemetry.UI

* Dashboard (metrics charts, service filters)
* Logs table with filters/search
* Trace explorer (timeline view)
* Export button (CSV/JSON)
* HTTP client to query API

---

## 📌 Optional Enhancements

| Feature            | Description                                     |
| ------------------ | ----------------------------------------------- |
| Local cache        | Use LiteDB or Redis for short-term memory cache |
| Export jobs        | Export to CSV/JSON on demand                    |
| Background rollups | Aggregate metrics per minute/hour               |
| Alerts (UI only)   | Show warning if certain thresholds hit          |

---

## 🚀 Dev/Deployment Setup

| Tool                    | Use                                                     |
| ----------------------- | ------------------------------------------------------- |
| Docker                  | PostgreSQL, your services                               |
| .env / appsettings.json | Central config                                          |
| dotnet user-secrets     | For local secret config (optional)                      |
| EF Core tools           | For migration scaffolding (if you mix EF for bootstrap) |
| Nginx (optional)        | Reverse proxy frontend/backend                          |

---

## 🔧 Project Goals Met

| Goal                     | Met By                       |
| ------------------------ | ---------------------------- |
| Real-time gRPC ingestion | `Telemetry.Api`              |
| High-speed write         | Dapper + PostgreSQL          |
| Custom UI                | Blazor WASM                  |
| Query capability         | Custom `/api/...` endpoints  |
| Offline + export         | Local DB + CSV/JSON          |
| Lightweight              | No external cloud dependency |

---

## ✅ Next Steps

1. Finalize DB schema and create `migrations.sql`
2. Define gRPC contracts
3. Implement ingestion server (`Telemetry.Api`)
4. Build Dapper insert logic
5. Expose read/query APIs
6. Develop Blazor UI
7. Add export + auth (if needed)

---
You're absolutely right — **Azure Application Insights** provides **automatic telemetry** (requests, dependencies, exceptions, etc.) without you having to manually log everything. To replicate **that ease of use** in your own self-hosted tool **AppTrace**, you need to design a few smart layers.

Here’s how you can **make AppTrace work like App Insights** — including **automatic capture of metrics, traces, and logs** without writing custom logic per app.

---

## ✅ Key Features of Application Insights You’re Referring To

1. **Automatic collection** of:

   * Requests (HTTP calls, gRPC)
   * Dependencies (SQL, HTTP calls to other APIs)
   * Exceptions
   * Custom events & metrics (if added)
   * Performance counters (CPU, memory, etc.)

2. **Context propagation** (trace ID, span ID, etc.)

3. **Correlation between services**

4. **Real-time metrics and logging without extra coding**

---

## ✅ How to Replicate That in AppTrace

### 1. **Use OpenTelemetry Auto-Instrumentation**

OpenTelemetry supports **automatic instrumentation** for .NET.

#### Install these NuGet packages in your app:

```sh
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Instrumentation.SqlClient
dotnet add package OpenTelemetry.Instrumentation.Runtime
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

This setup **automatically captures**:

* Incoming HTTP/gRPC requests
* Outgoing HTTP client requests
* SQL database calls
* Unhandled exceptions
* .NET runtime metrics (GC, threads, memory, etc.)

---

### 2. **Setup an OTLP gRPC Exporter in Your App**

Configure OpenTelemetry to export data to your AppTrace gRPC endpoint.

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter(opt =>
            {
                opt.Endpoint = new Uri("http://your-apptrace-ingestion-endpoint:4317");
                opt.Protocol = OtlpExportProtocol.Grpc;
            });
    })
    .WithMetrics(builder =>
    {
        builder.AddRuntimeInstrumentation()
               .AddAspNetCoreInstrumentation()
               .AddOtlpExporter();
    });
```

**No additional logic required per app or per controller.**

---

### 3. **Ensure Context Propagation Is Enabled**

This lets traces flow across microservices using `traceId` and `spanId`.

.NET apps already propagate this with B3 or W3C headers. OpenTelemetry handles this automatically.

---

### 4. **Use OpenTelemetry Resource Attributes**

This allows you to tag services with their name, environment, version, etc.

```csharp
services.ConfigureOpenTelemetryTracerProvider((sp, builder) =>
{
    builder.SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("UserService", serviceVersion: "1.0.0")
        .AddAttributes(new[] {
            new KeyValuePair<string, object>("deployment.environment", "prod")
        }));
});
```

---

### 5. **On AppTrace Ingestion Side:**

Your gRPC receiver must:

* Accept OTLP payloads
* Parse spans, metrics, and logs
* Store them in PostgreSQL via Dapper
* Optionally pre-aggregate metrics (avg response time, request count)

You **don’t need any per-service logic** in your apps beyond registering OpenTelemetry — just like App Insights.

---

## 🧠 Bonus: What App Insights Does That You Can Also Add Later

* **Live Metrics Stream** (via WebSocket or polling)
* **Custom Dashboards**
* **AI-based anomaly detection** (possible in future)
* **Log query language** (KQL-like or LINQ for SQL)

---

## ✅ Summary

| App Insights Feature     | AppTrace Equivalent Approach               |
| ------------------------ | ------------------------------------------ |
| Automatic HTTP tracing   | `OpenTelemetry.Instrumentation.AspNetCore` |
| SQL dependencies tracing | `OpenTelemetry.Instrumentation.SqlClient`  |
| Auto metrics collection  | `OpenTelemetry.Instrumentation.Runtime`    |
| Context propagation      | Built-in via W3C/B3 headers                |
| Export to backend        | OTLP gRPC to your AppTrace gRPC receiver   |
| Correlation of services  | Use Resource attributes + Trace Contexts   |
| Minimal setup            | Just register OpenTelemetry in DI          |

---

Let me know if you want a `.NET Starter Template` to bootstrap this for your microservices with this already wired in.


Great — let’s now **consolidate all AppTrace-specific components and configurations** you'll need to build your OpenTelemetry-compatible local observability tool — with **automatic instrumentation behavior** like Azure App Insights but fully self-hosted, lightweight, and performant.

---

## ✅ AppTrace — Project Consolidation Checklist

### 🧩 Core Capabilities

| Feature                   | Details                                                        |
| ------------------------- | -------------------------------------------------------------- |
| **Telemetry Types**       | Logs, Traces, Metrics                                          |
| **Data Ingestion**        | gRPC (OTLP)                                                    |
| **Storage**               | PostgreSQL via Dapper                                          |
| **Frontend**              | Blazor WebAssembly (query UI)                                  |
| **OpenTelemetry Support** | Full support for auto-instrumented traces, metrics, logs       |
| **Querying**              | Built-in trace/log/metric viewers with filters, search, export |
| **Export Options**        | JSON, CSV (via UI)                                             |
| **Offline Support**       | No cloud dependency; purely local                              |
| **Performance**           | High-write optimized, concurrency-safe design                  |

---

## 🏗 AppTrace Solution Structure

```
/AppTrace
│
├── /AppTrace.Collector          # gRPC Server (OpenTelemetry ingestion)
│   └── Receives traces, logs, metrics over OTLP gRPC
│
├── /AppTrace.Storage            # Dapper + PostgreSQL abstraction
│   └── Concurrent-safe inserts and queries
│
├── /AppTrace.UI                 # Blazor WebAssembly (WASM)
│   └── Query UI for logs, traces, metrics
│
├── /AppTrace.Common             # Shared DTOs, models, mappers
│
├── /AppTrace.Query.API          # Optional API for UI to access aggregated results
│   └── REST or gRPC-based querying
```

---

## 📦 Auto-Instrumentation Simulation (without NuGet)

You’ll enable **auto-instrumentation** at the service level via built-in OpenTelemetry extensions.

For any microservice to send data:

### 🛠 Required Configuration (in consuming services)

#### `Program.cs`

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri("http://localhost:4317"); // AppTrace Collector
            opt.Protocol = OtlpExportProtocol.Grpc;
        })
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService("MyServiceName"))
    )
    .WithMetrics(b => b
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri("http://localhost:4317");
            opt.Protocol = OtlpExportProtocol.Grpc;
        }));
```

> 💡 This mimics Azure App Insights’ “plug & play” experience — no manual tracking.

---

## 🗄 PostgreSQL Storage

Use **3 main tables**:

1. **Logs**

   ```sql
   CREATE TABLE logs (
     id UUID PRIMARY KEY,
     timestamp TIMESTAMPTZ,
     trace_id TEXT,
     span_id TEXT,
     severity TEXT,
     body TEXT,
     attributes JSONB
   );
   ```

2. **Traces**

   ```sql
   CREATE TABLE traces (
     id UUID PRIMARY KEY,
     trace_id TEXT,
     span_id TEXT,
     parent_span_id TEXT,
     name TEXT,
     start_time TIMESTAMPTZ,
     end_time TIMESTAMPTZ,
     attributes JSONB,
     status TEXT
   );
   ```

3. **Metrics**

   ```sql
   CREATE TABLE metrics (
     id UUID PRIMARY KEY,
     name TEXT,
     timestamp TIMESTAMPTZ,
     value DOUBLE PRECISION,
     attributes JSONB
   );
   ```

All inserts happen through **Dapper** with bulk/pooled connection patterns to avoid concurrency issues.

---

## 🔍 Query Support (from UI)

You’ll expose **read-only endpoints** (REST or gRPC) to serve frontend queries:

* `GET /logs?severity=Error&traceId=xyz`
* `GET /traces/{traceId}`
* `GET /metrics?name=request_duration`

These queries use parameterized Dapper calls with pagination + optional export format (CSV/JSON).

---

## 🧠 Auto-Instrumentation Strategy Summary

| Area            | OpenTelemetry Support                                |
| --------------- | ---------------------------------------------------- |
| HTTP pipeline   | ✅ `AspNetCoreInstrumentation()`                      |
| SQL             | ✅ `SqlClientInstrumentation()`                       |
| Logs            | ✅ via `ILogger<T>` and `OpenTelemetryLoggerProvider` |
| Metrics         | ✅ Runtime + custom counters                          |
| Export          | ✅ gRPC OTLP exporter                                 |
| Service context | ✅ `ResourceBuilder` with service name/env/version    |

You don't need to write custom logic in 90% of cases — that’s the "auto" magic.

---

## 🧰 Tooling & Dependencies

| Component        | Tool/Lib                                                                          |
| ---------------- | --------------------------------------------------------------------------------- |
| Collector (gRPC) | `Grpc.AspNetCore`, `OpenTelemetry.Proto.*`                                        |
| Storage          | `Dapper`, `Npgsql`                                                                |
| UI               | `Blazor WebAssembly`, `MudBlazor` (or plain)                                      |
| OpenTelemetry    | `OpenTelemetry`, `OpenTelemetry.Exporter.OTLP`, `OpenTelemetry.Instrumentation.*` |
| Config           | `appsettings.json`, environment overrides                                         |
| Serialization    | `System.Text.Json`, `protobuf-net` if needed                                      |
| Auth (optional)  | Azure AD, or local login if needed                                                |

---

## ✅ Next Steps to Build AppTrace

1. ✅ **Implement `AppTrace.Collector`**

   * gRPC server for traces, logs, metrics
   * Parse OTLP protobuf messages
   * Store via Dapper

2. ✅ **Build PostgreSQL schema**

   * Optimized tables, indexes

3. ✅ **Build `AppTrace.UI`**

   * Filter/query logs, traces, metrics
   * Export buttons
   * Live view optional

4. ✅ **Create shared models in `AppTrace.Common`**

5. ✅ **Write `AppTrace.Query.API`** (optional layer between UI and DB)

6. 🔒 **Add concurrency-safe Dapper usage** (e.g., batching, transactions)

7. ✅ **Test from a sample .NET app sending data**

---

Would you like:

* SQL scripts for all 3 telemetry types?
* Protobuf handling tips for OTLP message parsing in gRPC server?
* Query UX suggestions (like search, timeline view, etc)?

Let’s move step-by-step.

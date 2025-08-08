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

Would you like me to give you:

* A ready-made `.proto` for your gRPC ingestion?
* Dapper `INSERT` + `SELECT` query stubs?
* Blazor WASM project skeleton with UI stubbed pages?

Just say the word.

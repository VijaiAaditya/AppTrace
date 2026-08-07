# AppTrace architecture

This document describes the architecture of the current AppTrace implementation, including the modules, the runtime flow, and the design choices that were followed.

## Architectural goal

The project is built around one core idea:

- collect telemetry in a simple and standards-friendly way
- store it in a local-friendly backend
- expose it in a lightweight UI without depending on a cloud portal or KQL

That is why the architecture is intentionally modular and easy to reason about.

## High-level architecture

The current solution follows a layered pipeline:

```text
Instrumented app / OpenTelemetry exporter
		|
		v
AppTrace.Collector (gRPC OTLP receiver)
		|
		v
AppTrace.Storage (Dapper + PostgreSQL / in-memory abstraction)
		|
		v
AppTrace.Query.API (minimal API endpoints)
		|
		v
AppTrace.UI (Blazor WebAssembly UI)
```

## What we followed

The implementation follows a few consistent architectural principles:

1. Separation of concerns
   - ingestion, storage, query, and UI each live in separate projects
   - each layer has a clear responsibility

2. Local-first design
   - no external cloud dependency is required for the core experience
   - PostgreSQL and a local UI are enough to inspect telemetry

3. Standards-based ingestion
   - OTLP/gRPC is used because it is the standard protocol for OpenTelemetry data
   - this makes it easy for apps to export telemetry without custom integration code

4. Simplicity over complexity
   - Dapper is used instead of ORM-heavy approaches for explicit SQL and easier understanding
   - minimal APIs keep the query layer lightweight
   - Blazor WASM provides a simple browser-based interface without a separate frontend framework

5. Columnar tabular results for scale
   - the query layer returns a TabularResult object instead of one JSON object per record
   - this keeps payloads compact and makes the UI render efficiently for large page sizes

## Module responsibilities

### AppTrace.Collector

Purpose:
- receive telemetry over OTLP gRPC
- normalize incoming payloads into application-level models
- persist them by calling the storage abstractions

Responsibilities:
- expose gRPC services for logs, traces, and metrics
- convert OTLP resource and span data into internal entities
- extract service names and attributes from OTLP metadata
- log ingestion results for debugging and operational visibility

Key files:
- AppTrace.Collector/Program.cs
- AppTrace.Collector/Services/OtlpLogsService.cs
- AppTrace.Collector/Services/OtlpTraceService.cs
- AppTrace.Collector/Services/OtlpMetricsService.cs

### AppTrace.Storage

Purpose:
- abstract telemetry persistence behind a simple interface
- support multiple storage modes without changing the rest of the solution

Responsibilities:
- insert logs, traces, and metrics
- query logs, traces, and metrics
- support paging and search operations
- optionally use high-performance bulk insert logic

Storage modes:
- InMemory
  - useful for development and testing
- Standard
  - uses PostgreSQL through Dapper and Npgsql
- Bulk / HighPerformance
  - uses PostgreSqlBulkStorage with batching, COPY-based inserts, and retries

Key files:
- AppTrace.Storage/IStorageInterfaces.cs
- AppTrace.Storage/PostgreSqlStorage.cs
- AppTrace.Storage/PostgreSqlBulkStorage.cs
- AppTrace.Storage/StorageServiceCollectionExtensions.cs
- AppTrace.Storage/StorageOptions.cs

### AppTrace.Query.API

Purpose:
- expose read operations for the UI and any external caller
- return tabular data that is easy to render in a browser

Responsibilities:
- serve paged logs, traces, metrics
- search logs by plain text
- return CSV export data
- expose basic health and ingestion metrics endpoints

Key files:
- AppTrace.Query.API/Program.cs

### AppTrace.UI

Purpose:
- provide a simple web UI for browsing and exporting telemetry

Responsibilities:
- show logs, traces, and metrics in a tabular view
- support paging and search for logs
- allow CSV export via links
- call the query API through HttpClient

Key files:
- AppTrace.UI/Program.cs
- AppTrace.UI/Pages/Logs.razor
- AppTrace.UI/Pages/Traces.razor
- AppTrace.UI/Pages/Metrics.razor
- AppTrace.UI/Shared/TabularGrid.razor

### AppTrace.Common

Purpose:
- hold shared domain objects and common response models

Responsibilities:
- define LogEntry, TraceEntry, MetricEntry
- define TabularResult and conversion helpers

Key files:
- AppTrace.Common/Class1.cs
- AppTrace.Common/Models/TabularResult.cs

## Runtime flow

### 1. Telemetry enters the collector
An instrumented service sends telemetry to the collector over OTLP gRPC.

### 2. The collector transforms the payload
The collector services map OTLP records into AppTrace-specific entry types:
- LogEntry
- TraceEntry
- MetricEntry

The service name is taken from the OTLP resource attributes and added to the entry metadata.

### 3. Storage writes the data
The storage layer receives the entries and writes them to PostgreSQL.

For high-throughput scenarios, the bulk implementation:
- chunks data into batches
- uses PostgreSQL COPY for fast inserts
- falls back to parameterized inserts if COPY fails
- retries transient failures with backoff

### 4. The query API reads the data
The query API exposes REST endpoints that retrieve data from storage and return it as TabularResult payloads.

### 5. The UI renders and exports data
The UI calls the query API, renders the data in a table, and allows CSV export.

## Data model

The solution is centered on three telemetry concepts:

### Logs
Stored as a log entry with:
- id
- timestamp
- trace id
- span id
- severity
- body
- attributes
- service name

### Traces
Stored as a trace span with:
- id
- trace id
- span id
- parent span id
- name
- start time
- end time
- duration
- status
- attributes

### Metrics
Stored as a metric point with:
- id
- name
- timestamp
- value
- attributes

The schema uses PostgreSQL JSONB for attributes so the system can store dynamic OpenTelemetry-style metadata without requiring a rigid column structure.

## Why the architecture is suitable for this app

This architecture works well because it matches the needs of a local observability tool:

- small enough to understand quickly
- easy to run locally
- standard input protocol (OTLP)
- simple persistence model (PostgreSQL)
- fast enough for development and small-team usage

It is not designed to be a multi-tenant, globally distributed, or full-enterprise observability platform. It is a practical local-first tool with a clean architecture.

## Design decisions in practice

### gRPC for ingestion
gRPC is used because OTLP exporters expect it and because it is a natural fit for a high-throughput telemetry pipeline.

### Dapper for storage
Dapper keeps the storage layer explicit and lightweight. It is well suited to this project because the data model is relatively straightforward.

### Minimal APIs for query layer
Minimal APIs offer a lightweight way to expose simple query and export operations without introducing extra framework complexity.

### Blazor WebAssembly for UI
Blazor WASM keeps the UI in the same .NET ecosystem as the backend and makes local development simple.

### TabularResult for UI rendering
The TabularResult shape avoids returning large arrays of objects and makes it easy for the UI to render a paged grid efficiently.

## Developer workflow

### Local development
1. create and configure the PostgreSQL database
2. apply the schema from Design/database/schema.sql
3. start the collector
4. start the query API
5. start the UI
6. send telemetry from an instrumented app to the collector

### Adding a new feature
If a new capability is added, the preferred approach is:
- extend the collector if the pipeline needs to ingest a new telemetry shape
- extend storage if the persistence model needs a new field or query path
- extend the query API if the UI should expose the data
- extend the Blazor UI if the user should interact with the new data

## Current maturity and tradeoffs

The current implementation already covers the main path end to end:

- ingestion
- persistence
- query
- UI browsing
- CSV export

The main gaps are around operational maturity rather than core architecture:
- authentication
- rate limiting
- stronger health checks
- deployment automation
- migration tooling

These are the next logical improvements if the project becomes more production-like.

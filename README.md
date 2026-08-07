# AppTrace  -- Something is still in the kitchen => cooking, plz wait... !

AppTrace is a lightweight, ultrafast, easy to use, local-first observability tool designed to feel like Azure Application Insights without requiring Kusto Query Language or a cloud-hosted backend. It is built around a simple flow:

1. instrumented services send telemetry to an OTLP/gRPC collector
2. the collector normalizes logs, traces, and metrics
3. the storage layer persists them in PostgreSQL
4. the query API exposes paged data for browsing and export
5. a Blazor WebAssembly UI lets you inspect the data locally

This repo is intentionally simple: it prioritizes clarity, local execution, and low operational overhead over enterprise-scale features.

## Solution structure

The solution is split into the following projects:

- AppTrace.Collector
  - gRPC OTLP receiver for logs, traces, and metrics
  - converts OTLP payloads into internal models
  - forwards data to the storage layer

- AppTrace.Storage
  - PostgreSQL access through Dapper and Npgsql
  - supports multiple storage modes: InMemory, Standard, and Bulk/HighPerformance
  - includes batching, COPY-based bulk insert support, and retry behavior

- AppTrace.Query.API
  - minimal API host for querying and exporting telemetry
  - exposes logs, traces, metrics, health, and ingestion metrics endpoints

- AppTrace.UI
  - Blazor WebAssembly frontend for searching and browsing logs, traces, and metrics
  - supports CSV export from the UI

- AppTrace.Common
  - shared models such as LogEntry, TraceEntry, MetricEntry, and TabularResult

- Tests
  - storage and UI test projects for validating core behavior

## What the app does today

### Ingestion
- accepts telemetry over OTLP gRPC
- handles logs, traces, and metrics
- extracts service names and attributes from OTLP resources and spans
- stores data in PostgreSQL through the storage abstractions

### Storage
- uses PostgreSQL as the primary persistent store
- stores structured telemetry with JSONB attributes for dynamic metadata
- supports high-throughput inserts through bulk COPY operations when configured
- provides a simple storage abstraction so the same code can run with in-memory storage during local development

### Querying and browsing
- exposes paged endpoints for logs, traces, and metrics
- supports search over logs by body or attributes
- exposes CSV export for each telemetry type
- provides a lightweight UI with tabular views and paging

### Developer experience
- starts as a local, self-hosted observability stack
- avoids a cloud dependency and does not require KQL or a hosted portal
- follows an architecture that is easy to understand and extend

## Core features

- OTLP-compatible gRPC ingestion
- log, trace, and metric persistence
- PostgreSQL-backed storage with Dapper
- paged and searchable log browsing
- trace browsing by trace ID
- metric browsing by name
- CSV export from the query API and UI
- ingestion/health endpoints for basic operational visibility
- configurable storage mode for local or higher-throughput scenarios

## How it compares to Azure Application Insights

AppTrace is not a full replacement for every Azure Monitor feature yet. Its goal is narrower and more practical:

- provide a local-first place to inspect logs, traces, and metrics
- remove the need for cloud-hosted dashboards or KQL for simple debugging
- keep the implementation lightweight and easy to run on a developer machine

In other words, it is a simple self-hosted observability experience for local development and small-team usage.

## Prerequisites

- .NET 9 SDK
- PostgreSQL server
- a running database named apptrace (or another database you configure)

## Quick start

1. Create a PostgreSQL database and update the connection strings in:
   - AppTrace.Collector/appsettings.json
   - AppTrace.Query.API/appsettings.json

2. Apply the schema from:
   - Design/database/schema.sql

3. Start the collector:
   - dotnet run --project AppTrace.Collector

4. Start the query API:
   - dotnet run --project AppTrace.Query.API

5. Start the UI:
   - dotnet run --project AppTrace.UI

6. Point your instrumented app or local service at the collector endpoint using OTLP gRPC on port 4317.

7. Open the UI and browse the telemetry pages.

## Configuration notes

- AppTrace.Collector uses the AppTrace:StorageType setting to choose the storage implementation.
- The default storage mode is bulk, which uses the high-performance PostgreSQL backend.
- The UI reads the query API base URL from AppTrace.UI/wwwroot/appsettings.json.

## Suggested usage pattern

A typical setup looks like this:

- an application or service emits OpenTelemetry data
- the data is exported to AppTrace.Collector over gRPC
- the collector stores it in PostgreSQL
- a developer opens the Blazor UI and inspects recent logs and traces without needing a cloud portal

## Current maturity

The app already has the core pipeline in place:

- ingest
- persist
- query
- render
- export

What is still missing for production-style use is mainly operational hardening such as authentication, rate limiting, stronger health checks, and deployment automation. The current focus is on a clean local-first architecture rather than a full enterprise platform.

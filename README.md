## 🛰️ AppTrace

> **AppTrace** is a blazing-fast, local-first observability toolkit for capturing, storing, and querying **OpenTelemetry-compliant logs, traces, and metrics**. Built with .NET 9, Blazor WebAssembly, gRPC, Dapper, and PostgreSQL — designed to be minimal, dependency-free, and production-ready.

---

### 🌟 Features

* ✅ **Local-first & Cloud-free** – Store all telemetry data locally with no vendor lock-in.
* 🔍 **Queryable UI** – Blazor WebAssembly frontend with trace explorer, log viewer, and metric dashboards.
* ⚡ **High-performance ingestion** – gRPC intake pipeline with Dapper + PostgreSQL backend.
* 📊 **Support for all telemetry types** – Logs, traces, and metrics handled natively.
* 🔐 **Optional Azure AD Integration** – Simple auth flow via Azure App Registration.
* 📦 **Reusable Project Library** – Can be embedded in any microservice ecosystem.

---

### 📦 Tech Stack

* **.NET 9** for backend ingestion & API
* **Blazor WebAssembly** frontend (SPA)
* **gRPC** for high-throughput telemetry intake
* **Dapper** for fast, lightweight SQL access
* **PostgreSQL** for structured telemetry storage
* **OpenTelemetry Protocol (OTLP)** compatible

---

### 🔧 Use Cases

* Self-hosted observability in **resource-constrained** environments
* Local developer telemetry during **integration testing**
* Lightweight telemetry collection for **microservice-based** apps
* Embedding observability into **internal developer portals**

---

### 🛠️ Coming Soon

* ⏱️ Time-windowed metric aggregations
* 🔭 Distributed span explorer
* 📤 Export to OTLP / JSON / CSV
* 🧩 Plugin system for storage adapters
* 📈 Grafana-compatible export layer (optional)

---

### 📖 License

MIT License — free to use, fork, and extend.


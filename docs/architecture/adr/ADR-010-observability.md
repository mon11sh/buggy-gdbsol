# ADR-010 — Observability: Correlation, Distributed Tracing, and Metrics

- **Status:** Accepted
- **Related:** ADR-007 (composition root), ADR-009 (contract stability), system-architecture §6, operations/observability

## Context
The platform is a set of independent ASP.NET Core Web API services that call one another
synchronously. To operate it we need to (a) follow a single request across
service boundaries, (b) see structured logs we can query, and (c) measure traffic
and latency per service — **without** breaking the frozen API contracts
([ADR-009](ADR-009-api-contract-stability.md)) and **without** adding a heavy
agent to each of the ~10 service virtual-environments.

Constraints that shaped the decision:

- **No new heavyweight dependencies.** Each service has its own `venv`; pulling
  `opentelemetry-*` or `prometheus-client` into all of them is avoidable weight.
- **Contract-safe.** Instrumentation must not appear in any service's OpenAPI or
  change any response body consumers read.
- **Uniform.** Every service should behave identically — one implementation in
  the shared `gdb_common` library, wired the same way in every `main.py`.

## Decision
**Implement observability as dependency-free primitives in `gdb_common`, wired
into every service through two one-line installers.** Use industry-standard
**wire formats** (W3C Trace Context, Prometheus text exposition) implemented
directly, rather than importing framework SDKs.

### 1. Correlation + structured logging (`gdb_common.observability`)
- `install_observability(app)` adds middleware that reads inbound
  `X-Correlation-ID` (or mints a UUID), binds it to a `ContextVar`, attaches it
  to every log record, echoes it on the response, and applies security headers.
- Logs are emitted as **JSON** (`JsonFormatter`) including `correlation_id`,
  `trace_id`, `span_id`, level, logger, and message.

### 2. Distributed tracing — W3C Trace Context (`gdb_common.observability`)
- The middleware parses the inbound **`traceparent`** header. If present and
  valid, the trace is **continued** (same 32-hex `trace_id`); otherwise a new
  `trace_id` is started. Each hop mints a fresh 16-hex `span_id`.
- `trace_id`/`span_id` are exposed via `current_trace_id()` / `current_span_id()`
  and included in every log line.
- `traceparent_header()` builds the outbound header; **all 13 inter-service call
  sites** forward it alongside `X-Internal-API-Key` and `X-Correlation-ID`, so a
  trace stitches across services.

### 3. Metrics — Prometheus, dependency-free (`gdb_common.metrics`)
- `install_metrics(app, *, path="/metrics", service=None)` adds middleware and a
  `/metrics` endpoint rendered in the Prometheus text format by a small
  thread-safe in-process registry (no `prometheus-client` dependency).
- Series exposed:
  - `http_requests_total{service,method,path,status}` — counter
  - `http_request_duration_seconds` — histogram (11 buckets)
  - `http_requests_in_progress` — gauge
- **Cardinality is bounded** — the `path` label is the route **template**
  (`request.scope["route"].path`, e.g. `/api/v1/accounts/{id}`), never the raw
  path, so identifiers cannot explode the series count.
- `/metrics` uses **`include_in_schema=False`** so it never appears in OpenAPI.

### Wiring (identical in every service `main.py`)
```python
install_observability(app)
install_metrics(app)
install_exception_handlers(app)
```

## Consequences
**Positive**
- One request is traceable end-to-end (correlation id + W3C trace) across all services.
- Uniform, queryable JSON logs and Prometheus metrics with **zero** new third-party runtime dependencies.
- **Contract-safe:** OpenAPI snapshots are unchanged (`/metrics` excluded); the OpenAPI gate stays green.
- Standard wire formats → any Prometheus scraper / W3C-aware tracer interoperates without custom exporters.

**Negative**
- We maintain small amounts of standard-format code (histogram bucketing, traceparent parse/format) ourselves instead of delegating to an SDK. Mitigation: covered by `libs/gdb_common/tests/test_observability_metrics.py` (7 tests) and kept minimal.
- No sampling / no span export to a collector yet — traces live in logs. A future ADR can add an OTLP exporter behind the same `gdb_common` seam without touching services.

## Alternatives considered
- **OpenTelemetry SDK + auto-instrumentation in every service:** rejected for
  now — adds heavy dependencies to ~10 venvs and a collector to the deployment,
  disproportionate for a training platform; can be adopted later behind the same
  installer seam.
- **`prometheus-client` library:** rejected — a small dependency-free registry
  covers our three metric families and avoids per-venv installs; the exposition
  format is identical to scrapers.
- **Log-only correlation (no W3C trace):** rejected — a standard `traceparent`
  lets external tracers stitch spans and future-proofs an OTLP migration.



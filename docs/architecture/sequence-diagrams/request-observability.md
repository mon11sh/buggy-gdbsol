# Sequence — Cross-cutting: one request, end to end

Every request passes through the same `gdb_common` middleware. This shows how a
**correlation ID**, **W3C trace context**, **metrics**, and the **error
envelope** are applied to any request — and propagated across a downstream call.

```mermaid
sequenceDiagram
    autonumber
    actor U as Client
    participant GW as central_gateway
    participant S1 as service A<br/>(gdb_common middleware)
    participant S2 as service B<br/>(gdb_common middleware)

    U->>GW: HTTP request (maybe X-Correlation-ID / traceparent)
    GW->>S1: proxy request

    rect rgb(240,240,255)
    Note over S1: install_observability middleware
    S1->>S1: correlation_id = inbound X-Correlation-ID or new uuid
    S1->>S1: parse inbound traceparent →<br/>continue trace / start new; mint span_id
    S1->>S1: bind {correlation_id, trace_id, span_id} to context
    end

    rect rgb(240,255,240)
    Note over S1: install_metrics middleware
    S1->>S1: http_requests_in_progress++ ; start timer
    end

    Note over S1: handler runs — every log line is JSON<br/>with correlation_id + trace_id + span_id

    S1->>S2: downstream call<br/>(forwards X-Correlation-ID + traceparent + X-Internal-API-Key)
    Note over S2: same middleware → same correlation_id,<br/>same trace_id, child span_id
    S2-->>S1: response

    alt handler raises
        S1->>S1: exception handler → safe JSON envelope<br/>(no stack/details leaked)
    end

    S1->>S1: metrics: duration observed; in_progress-- ;<br/>http_requests_total{service,method,path,status}++
    S1-->>GW: response (+ X-Correlation-ID, traceparent, security headers)
    GW-->>U: response
```

## What each layer contributes

| Middleware | Adds | Where |
|---|---|---|
| **Observability** | Correlation ID (in/out), W3C trace context (`traceparent`, `trace_id`/`span_id`), structured JSON logs, security headers | `gdb_common.observability.install_observability(app)` |
| **Metrics** | `http_requests_total`, `http_request_duration_seconds` (histogram), `http_requests_in_progress` (gauge); `/metrics` endpoint | `gdb_common.metrics.install_metrics(app)` |
| **Exceptions** | Safe error envelope — consistent shape, no internal detail leakage | `gdb_common.exceptions.install_exception_handlers(app)` |

## Key properties

- **Correlation ID** is read from the inbound `X-Correlation-ID` or minted; it is
  bound to the request context, attached to every log line, echoed in the
  response, and **forwarded** on every downstream call.
- **Trace context** follows [W3C Trace Context](https://www.w3.org/TR/trace-context/):
  an inbound `traceparent` continues the trace; otherwise a new 32-hex `trace_id`
  is started. Each hop mints a fresh 16-hex `span_id`. So one logical operation
  stitches together across services.
- **Metric cardinality is bounded** — the label uses the route **template**
  (`/api/v1/accounts/{id}`), not the raw path, so IDs don't explode the series.
- **`/metrics` is excluded from OpenAPI** (`include_in_schema=False`), so scraping
  never changes the API contract.

See [operations/observability.md](../../operations/observability.md) for querying
logs/metrics/traces at runtime.



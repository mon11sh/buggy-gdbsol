# Runbook — Observability (Logs & Tracing)

How to follow a request across the platform at runtime using **correlation IDs**,
**structured JSON logs**, and **W3C distributed tracing** — all provided by
`gdb_common.observability` ([ADR-010](../architecture/adr/ADR-010-observability.md)).

## The three signals

| Signal | What it gives you | Where |
|---|---|---|
| **Correlation ID** | one id per client request, across all hops | `X-Correlation-ID` header + every log line |
| **Trace context** | W3C `trace_id` + per-hop `span_id` | `traceparent` header + every log line |
| **Metrics** | rates/latency/in-flight | `/metrics` (see [health-and-metrics.md](health-and-metrics.md)) |

## Structured logs

Every log line is JSON and includes `correlation_id`, `trace_id`, `span_id`,
level, logger, and message. Example (fields shown):

```json
{"level":"INFO","logger":"app.services.transfer_service","message":"[SUCCESS] Transfer successful: Transaction ID 123",
 "correlation_id":"7b3f...","trace_id":"4bf92f3577b34da6a3ce929d0e0e4736","span_id":"00f067aa0ba902b7"}
```

### Follow one request

1. Grab the `X-Correlation-ID` from the client response (or set your own on the
   request — the platform honors an inbound one).
2. Filter logs across **all** services by that id:

```powershell
# per service window / log file
Select-String -Path .\*_service\logs\*.log -Pattern '"correlation_id":"7b3f'
```

Because the id is minted at the edge and **forwarded on every inter-service call**,
one filter shows the whole fan-out (e.g. transactions → accounts → gateway →
notification).

## Distributed tracing (W3C Trace Context)

- Inbound `traceparent` is parsed; if valid the **trace continues** (same
  `trace_id`), else a new 32-hex `trace_id` starts. Each hop mints a fresh 16-hex
  `span_id`.
- `traceparent` is **echoed** on responses and **forwarded** on all 13
  inter-service calls, so spans stitch into one trace.
- Today traces live **in the logs** (via `trace_id`/`span_id`). Because the wire
  format is standard W3C, a future OTLP exporter can be added behind the same
  `gdb_common` seam without touching services (see ADR-010 "Negative/Alternatives").

### Correlate logs → trace

```promql
# find slow requests in metrics, then...
histogram_quantile(0.95, sum by (le, path) (rate(http_request_duration_seconds_bucket[5m])))
```

```powershell
# ...pull the matching trace_id from the logs for that route and follow it across services
Select-String -Path .\*_service\logs\*.log -Pattern '"trace_id":"4bf92f35'
```

## Context propagation cheat-sheet

| Header | Direction | Meaning |
|---|---|---|
| `X-Correlation-ID` | in + out | one id per client request |
| `traceparent` | in + out | W3C trace context (`version-traceid-spanid-flags`) |
| `X-Internal-API-Key` | out (inter-service) | authenticates service-to-service calls |
| `Authorization: Bearer` | in (end-user) | JWT + RBAC |

## Related

- [health-and-metrics.md](health-and-metrics.md) · [troubleshooting.md](troubleshooting.md)
- [request-observability sequence](../architecture/sequence-diagrams/request-observability.md) · [ADR-010](../architecture/adr/ADR-010-observability.md)



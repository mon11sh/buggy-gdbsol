# Operations

Runbooks for operating the GDB platform: health checks, metrics, logs/tracing,
and troubleshooting. Everything here is provided uniformly by `gdb_common` and is
identical across all services.

| Runbook | Purpose |
|---|---|
| [health-and-metrics.md](health-and-metrics.md) | Health/liveness/readiness probes and the Prometheus `/metrics` endpoint |
| [observability.md](observability.md) | Correlation IDs, structured JSON logs, and W3C distributed tracing at runtime |
| [troubleshooting.md](troubleshooting.md) | Common failures and how to diagnose them |

## Endpoints on every service

| Path | Purpose | In OpenAPI? |
|---|---|---|
| `/health` | Overall health | no |
| `/live` | Liveness (process is up) | no |
| `/ready` | Readiness (ready for traffic) | no |
| `/metrics` | Prometheus metrics | no |
| `/docs`, `/redoc`, `/openapi.json` | API docs / schema | — |

The probe + metrics endpoints use `include_in_schema=False`, so scraping and
orchestration never affect the API contract ([ADR-009](../architecture/adr/ADR-009-api-contract-stability.md),
[ADR-010](../architecture/adr/ADR-010-observability.md)).

## Design reference

See [ADR-010 — Observability](../architecture/adr/ADR-010-observability.md) for
why observability is dependency-free `gdb_common` code, and the
[request-observability sequence](../architecture/sequence-diagrams/request-observability.md)
for how it applies per request.



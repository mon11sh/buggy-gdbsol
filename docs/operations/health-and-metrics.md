# Runbook — Health & Metrics

Every service exposes uniform health probes and a Prometheus metrics endpoint,
wired by `gdb_common` (`install_observability` + `install_metrics`).

## Health probes

| Endpoint | Meaning | Use it for |
|---|---|---|
| `GET /health` | Overall health snapshot | dashboards, manual checks |
| `GET /live` | **Liveness** — the process is up (`{"status":"alive"}`) | Kubernetes `livenessProbe`; restart if failing |
| `GET /ready` | **Readiness** — ready to accept traffic (`{"status":"ready"}`) | Kubernetes `readinessProbe`; remove from LB if failing |

```powershell
curl http://localhost:8001/health
curl http://localhost:8001/live
curl http://localhost:8001/ready
```

All three are `include_in_schema=False` (not in OpenAPI).

**Liveness vs readiness:** a failing **liveness** means "restart me"; a failing
**readiness** means "don't send me traffic yet" (e.g. DB not reachable at
startup). Wire them separately in the orchestrator so a slow dependency doesn't
cause a restart loop.

## Metrics — `GET /metrics`

Prometheus text exposition, rendered by a dependency-free in-process registry.

```powershell
curl http://localhost:8001/metrics
```

### Series exposed

| Metric | Type | Labels | Meaning |
|---|---|---|---|
| `http_requests_total` | counter | `service`, `method`, `path`, `status` | total requests |
| `http_request_duration_seconds` | histogram | `service`, `method`, `path` | latency (11 buckets) + `_sum`/`_count` |
| `http_requests_in_progress` | gauge | `service` | requests currently being handled |

- **`path` is the route template** (`/api/v1/accounts/{id}`), never the raw path —
  so per-ID cardinality can't explode the series.
- `/metrics` itself is excluded from OpenAPI (`include_in_schema=False`).

### Scraping with Prometheus

```yaml
scrape_configs:
  - job_name: gdb
    metrics_path: /metrics
    static_configs:
      - targets:
          - localhost:8001   # accounts
          - localhost:8002   # transactions
          - localhost:8003   # users
          - localhost:8004   # auth
          - localhost:8005   # aadhar
          - localhost:8006   # company_crv
          - localhost:8007   # notification
          - localhost:8008   # payment_gateway
          - localhost:8000   # gateway
          - localhost:8010   # registry
```

### Useful PromQL

```promql
# request rate per service
sum by (service) (rate(http_requests_total[5m]))

# error rate (5xx) per service
sum by (service) (rate(http_requests_total{status=~"5.."}[5m]))

# p95 latency per route
histogram_quantile(0.95, sum by (le, path) (rate(http_request_duration_seconds_bucket[5m])))

# in-flight requests
http_requests_in_progress
```

## Suggested alerts

| Alert | Condition (sketch) |
|---|---|
| Service down | `up == 0` or `/ready` failing |
| High error rate | 5xx rate > 5% of total for 5m |
| Latency regression | p95 `http_request_duration_seconds` above SLO for 10m |
| Saturation | `http_requests_in_progress` sustained near capacity |

## Related

- [observability.md](observability.md) (logs + tracing) · [troubleshooting.md](troubleshooting.md)
- [ADR-010](../architecture/adr/ADR-010-observability.md)



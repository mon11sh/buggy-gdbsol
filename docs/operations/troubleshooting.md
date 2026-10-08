# Runbook — Troubleshooting

Common failures and how to diagnose them. Start by finding the request's
**correlation ID** and filtering logs across services
([observability.md](observability.md)).

## Startup & ports

| Symptom | Likely cause | Fix |
|---|---|---|
| `address already in use` on restart | old service window/process still holds the port | Fully close old windows before re-running `run_all.py`; kill the stray PID |
| Service window flashes and closes | crash on startup (bad DB creds, missing dep) | Windows stay open by design — read the error in the window; check `.env` |
| Service starts but `/ready` fails | DB unreachable at startup | Verify DB is running + `DATABASE_URL`; readiness clears once DB is reachable |

## Database / providers

| Symptom | Likely cause | Fix |
|---|---|---|
| `ModuleNotFoundError: asyncpg/aiomysql/aiosqlite` | driver not installed in that service's venv | re-run `python Gdb.Setup`; confirm provider→driver ([switching-databases.md](../development/switching-databases.md)) |
| Wrong DB used after switching | leftover `DATABASE_URL` overriding provider | `setup_provider.py` clears `DATABASE_URL`; re-run it, or clear it in `.env` |
| "table does not exist" (production) | relied on dev `create_all` | run EF Core Migrations migrations — EF Core Migrations is authoritative ([ADR-008](../architecture/adr/ADR-008-database-migration-strategy.md)) |
| Data vanished after restart | provider is `inmemory` | expected — use `sqlite`/`postgres`/… for persistence |

## Inter-service calls

| Symptom | Likely cause | Fix |
|---|---|---|
| `401/403` on an internal call | missing/wrong `X-Internal-API-Key` | ensure `INTERNAL_API_KEY` matches across caller + callee |
| Transfer fails at gateway step (402) | payment gateway rejected or unreachable | client fails **closed** by design; check `central_payment_gateway` + logs |
| Login fails though creds look right | `auth`→`users` verify call failing | check `users_service` `/internal/v1/users/verify`; follow the correlation id |
| Downstream service not found | registry has no live entry | confirm the target registered/heartbeats to `registry_service` (:8010) |

## Transfers (saga specifics)

| Symptom | Meaning | Action |
|---|---|---|
| `409` on transfer | duplicate `Idempotency-Key` still in progress | expected — retry with the same key returns the stored result once complete |
| Same key "replays" old result | idempotent replay — transfer already executed | expected; not a double-charge |
| `CRITICAL: reversal FAILED ...` in logs | credit failed post-debit **and** the compensating refund failed | **manual reconciliation** required for that source account/amount |
| Money debited but no ledger row | commit failed after money moved | idempotency key is intentionally **not** released; investigate + reconcile |

## Contracts / tests / gates

| Symptom | Likely cause | Fix |
|---|---|---|
| OpenAPI gate fails after a "no-op" refactor | you changed a **docstring** (ASP.NET Core Web API uses it as `description`) | restore the docstring verbatim ([testing.md](../development/testing.md)) |
| OpenAPI gate fails after a real new endpoint | expected — contract changed | review diff is **additive**, then `--update` the snapshot |
| `TestClient` error: `unexpected keyword argument 'app'` | `httpx` too new for Starlette 0.27 | pin `httpx==0.25.1` |
| Architecture gate fails | `domain/` imported a framework or `services/` imported `app.infrastructure` | move the import to the correct layer |

## Observability itself

| Symptom | Likely cause | Fix |
|---|---|---|
| Logs missing `correlation_id`/`trace_id` | `install_observability(app)` not wired | ensure it's called in that service's `main.py` |
| `/metrics` 404 | `install_metrics(app)` not wired | add it after `install_observability(app)` |
| Trace doesn't span services | `traceparent` not forwarded on a call | inter-service clients must send `traceparent_header()` (all 13 sites do) |
| Metric series exploding | raw path used as a label | must use the route **template**; the middleware already does this |

## First moves for any incident

1. Get the **correlation ID** from the failing response.
2. Filter logs across services by it (see [observability.md](observability.md)).
3. Check the involved services' `/health` + `/ready`.
4. Check `/metrics` for error-rate/latency spikes ([health-and-metrics.md](health-and-metrics.md)).
5. For money movement, search logs for `CRITICAL` / reversal messages before assuming loss.



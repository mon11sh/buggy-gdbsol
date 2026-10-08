# GDB Microservice Design Patterns

Five microservice patterns are implemented across the GDB platform. Four ship as
new, reusable, tested code (mostly in the shared `gdb_common` package); the fifth
(Database per Service) is an architectural property the system already satisfies —
documented and verified here.

| # | Pattern | Where | Tests |
|---|---------|-------|-------|
| 1 | Circuit Breaker | `libs/gdb_common/gdb_common/resilience.py` (wired into transactions→accounts) | `accounts_service/tests/test_circuit_breaker.py` (3) |
| 2 | Service Discovery | `libs/gdb_common/gdb_common/discovery.py` + `registry_service/` | `accounts_service/tests/test_service_discovery.py` (5) |
| 3 | CQRS | `libs/gdb_common/gdb_common/cqrs.py` + `transactions_service/app/cqrs/` | `test_cqrs.py` (6) + `test_cqrs_transactions.py` (3) |
| 4 | API Gateway | `central_gateway_service/` | `central_gateway_service/tests/test_gateway.py` (6) |
| 5 | Database per Service | every service owns its schema; cross-service access via HTTP | verified below |

---

## 1. Circuit Breaker

Stops a caller from hammering a failing dependency. `CircuitBreaker` tracks
consecutive failures; after `failure_threshold` it trips **OPEN** and fails fast
with `CircuitBreakerOpen` for `reset_timeout` seconds, then tries a single
**HALF_OPEN** probe — success closes it, failure re-opens it.

Wired at the transactions → accounts boundary
(`transactions_service/app/integration/account_service_client.py`): a module-level
breaker wraps the account-validation call and surfaces a
`ServiceUnavailableException` when open, so a down accounts service degrades
gracefully instead of cascading.

```python
from gdb_common.resilience import CircuitBreaker, CircuitBreakerOpen
breaker = CircuitBreaker("accounts-service", failure_threshold=5, reset_timeout=30.0)
result = await breaker.call(lambda: client.get(url))
```

## 2. Service Discovery

Services register a logical name → URL instead of everyone hard-coding ports.
`ServiceRegistry` supports register / heartbeat / TTL-expiry / round-robin
`resolve`; `StaticResolver` is the config-based fallback. `create_registry_app()`
exposes the registry over HTTP and is deployed as **`registry_service`** (:8010):

```
POST /register {name,url,ttl}   POST /heartbeat   DELETE /register
GET  /resolve/{name} -> {url}    GET  /services    GET /health
```

## 3. CQRS (Command Query Responsibility Segregation)

The write path and read path are modelled as separate messages on separate buses.
`CommandBus`/`QueryBus` **enforce** the split — registering a `Query` on the
command bus (or vice-versa) raises `TypeError`.

The transactions domain (`transactions_service/app/cqrs/`) defines
`DepositCommand` / `WithdrawCommand` / `TransferCommand` (write) and
`GetTransferLimitQuery` / `GetAllTransferRulesQuery` (read).
`build_transaction_buses()` wires handlers to the existing service singletons —
segregation without duplicating business logic.

## 4. API Gateway

`central_gateway_service` (:8000) is the single public entry point. Path-based
routing: the first path segment is the logical service, the remainder is forwarded
verbatim.

```
GET  /accounts/api/v1/accounts/101  ->  accounts backend  /api/v1/accounts/101
POST /auth/api/v1/auth/login        ->  auth backend      /api/v1/auth/login
```

Backends are located through the Service-Discovery `StaticResolver`
(`GATEWAY_<NAME>_URL` env overrides let one image serve local **and** Docker). The
gateway centralises CORS and correlation-id propagation, strips hop-by-hop
headers, returns 404 for an unknown service and 502 for an unreachable backend,
and passes upstream status through unchanged.

## 5. Database per Service

Each service owns its data; no service reaches into another's tables.

**Evidence (verified in this repo):**

- **Separate schemas / engines** — each stateful service has its own
  `app/database/` (own `orm_models.py`, own engine/session) and its own database
  name: `accounts → gdb_accounts_db`, `auth → gdb_auth_db`, `users → gdb_users_db`,
  `transactions → its own transactions DB`. The aadhar / company / notification /
  payment services are stateless (no DB).
- **No shared-DB coupling** — no service imports another service's
  repositories or ORM models (grep across the tree is clean).
- **Cross-service data is fetched over HTTP**, never by querying a peer's DB.
  Integration (HTTP) clients:
  - `accounts` → aadhar, company, notification
  - `transactions` → accounts, notification, payment
  - `auth` → users
- **Independent migrations** — each service carries its own EF Core Migrations
  `EF Core Migrations/` + `versions/` chain, so schemas evolve independently.

This isolation is what makes the other four patterns meaningful: services are
independently deployable units that communicate only through APIs — which is
exactly why they need a gateway, discovery, circuit breakers, and per-service
read/write modelling.

---

## Running & testing the patterns

```bash
# Circuit Breaker + Service Discovery + CQRS bus tests
cd accounts_service     && DATABASE_PROVIDER=inmemory dotnet test gdb-service-dotnet.slnx \
  tests/test_circuit_breaker.py tests/test_service_discovery.py tests/test_cqrs.py -q

# CQRS transactions wiring
cd transactions_service && DATABASE_PROVIDER=inmemory dotnet test gdb-service-dotnet.slnx \
  tests/test_cqrs_transactions.py -q

# API Gateway (uses the accounts venv, which has httpx + ASP.NET Core Web API + gdb_common)
cd central_gateway_service && ../accounts_service/dotnet test gdb-service-dotnet.slnx tests/ -q

# Run the two new standalone services
cd registry_service       && dotnet run app.main:app --port 8010
cd central_gateway_service && dotnet run app.main:app --port 8000
```

All 23 pattern tests pass (3 + 5 + 6 + 3 + 6).



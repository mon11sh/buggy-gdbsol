# System Architecture

The **Global Digital Bank (GDB)** platform is a set of ASP.NET Core Web API microservices that
share a common library (`gdb_common`) and a common architectural standard. This
document describes the **implemented** architecture.

## 1. Architectural style

- **Clean / Hexagonal Architecture** with **DDD-lite**. Dependencies point
  **inward only**: the domain depends on nothing; the application (use cases)
  depends on the domain; infrastructure depends on the application/domain.
- **Ports & Adapters** for outbound collaborators (other services) — the domain
  declares ports; `integration/` HTTP clients implement them.
- **Repository + Unit of Work** for persistence — a session-owning UoW commits
  once per use case (see [ADR-004](adr/ADR-004-unit-of-work.md)).
- **Composition Root + Dependency Injection** — one place wires the object graph;
  ASP.NET Core Web API async-generator dependencies build a use case per request and release
  the Unit of Work afterwards ([ADR-007](adr/ADR-007-composition-root.md)).

The reference implementation is **`accounts_service`**.

## 2. Service tiers

Not every service needs the same machinery. Applying "no unnecessary
abstractions", services fall into three tiers:

| Tier | Services | Port | Owns a DB? | Structure |
|---|---|---|---|---|
| **Gold** — full clean architecture | `accounts_service` | 8001 | ✅ | full stack (below) |
| | `transactions_service` | 8002 | ✅ | full stack |
| | `users_service` | 8003 | ✅ | full stack |
| | `auth_service` | 8004 | ✅ | full stack + `security/` |
| **Lite** — stateless stubs | `aadhar_service` | 8005 | ❌ | `api` + `dto` + service |
| | `company_crv_service` | 8006 | ❌ | `api` + `dto` + service |
| | `notification_service` | 8007 | ❌ | `api` + `dto` + service |
| | `central_payment_gateway_service` | 8008 | ❌ | `api` + `dto` + service |
| **Edge** — infrastructure | `central_gateway_service` (API gateway) | 8000 | ❌ | thin wrapper over `gdb_common` |
| | `registry_service` (service discovery) | 8010 | ❌ | thin wrapper over `gdb_common` |

The **lite** stubs simulate external systems (UIDAI, MCA, a payment gateway,
notifications); they have no domain or persistence, so they intentionally omit
`domain/`, `repositories/`, and the Unit of Work.

## 3. The gold-standard folder structure

Every stateful service mirrors `accounts_service`:

```
app/
├── api/                     # ASP.NET Core Web API routers (thin; auth + serialization only)
├── dto/                     # request/response schemas (C# DTOs)
├── services/                # thin use cases + services/unit_of_work.py (UoW contract)
├── domain/                  # framework-free business core
│   ├── models/              #   value objects, entities/aggregates, enums
│   ├── rules.py             #   business rules (single source of truth)
│   └── ports.py             #   outbound ports (abstract collaborators)
├── mapping/                 # explicit DTO ↔ domain ↔ ORM mapping
├── repositories/
│   └── interfaces/          # ONE official repository contract per aggregate (ABCs)
├── infrastructure/
│   └── persistence/
│       ├── orm_models.py    # Entity Framework Core ORM (portable across dialects)
│       ├── connection.py    # provider bootstrap (get_provider / init_db / close_db)
│       ├── unit_of_work.py  # Entity Framework CoreUnitOfWork + InMemoryUnitOfWork
│       ├── providers/       # base, factory, Entity Framework Core_provider, inmemory_provider
│       ├── repositories/    # Entity Framework Core + InMemory concrete repositories
│       └── seed_data.py     # default/seed data
├── composition/             # composition root (wires the object graph)
├── dependencies/            # ASP.NET Core Web API DI providers (async-generator, aclose UoW)
├── config/                  # settings (12-factor env)
├── exceptions/              # domain/application exception taxonomy
├── integration/             # outbound HTTP clients (implement domain ports)
└── main.py                  # app assembly + gdb_common wiring
```

### The dependency rule (enforced by the architecture gate)

```
api → dto → services (use cases) → domain (models + rules + ports)
                       │                       ▲
                       ▼                       │  implements
     mapping → repository interfaces      infrastructure (Entity Framework Core / InMemory via UoW)
                                                │
                                                ▼
                                             Database
```

- `domain/` imports **no** framework (no ASP.NET Core Web API / Entity Framework Core / C# DTOs / httpx).
- `services/` import **no** `app.infrastructure`.
- These are checked by `libs/gdb_common/tests/test_architecture.py` for every
  service in `CONVERTED_SERVICES` (accounts, transactions, users, auth).

## 4. Communication patterns

- **Synchronous HTTP (JSON)** between services. Inter-service calls are
  authenticated with an **internal API key** (`X-Internal-API-Key`); end-user
  calls use **JWT bearer tokens** (RS256/HS256) with RBAC.
- **Ports & Adapters**: a service's domain declares a port (e.g.
  `AccountServicePort`); the `integration/` HTTP client implements it; the
  composition root injects it. Use cases never import a concrete client.
- **Resilience**: outbound clients are wrapped with a **circuit breaker**
  (`gdb_common.CircuitBreaker`) and timeouts; failures fail closed/compensate.
- **Service discovery**: `registry_service` (backed by `gdb_common.discovery`) —
  services register/heartbeat; clients resolve a logical name to a live URL.
- **API gateway**: `central_gateway_service` fronts the platform (routing/proxy,
  CORS allow-list, rate limiting).
- **Context propagation**: every call forwards `X-Correlation-ID` and W3C
  `traceparent`, so logs and traces stitch together across services.

Who calls whom (outbound dependencies):

| Service | Calls |
|---|---|
| accounts | aadhar, company_crv, notification |
| transactions | accounts, central_payment_gateway, notification |
| auth | users |
| central_gateway | all backends (proxy) |

## 5. Persistence & multi-database support

- Selected by config: `DATABASE_PROVIDER ∈ {inmemory, sqlite, mysql, postgres, supabase}`.
- **One** Entity Framework Core implementation serves sqlite/mysql/postgres/supabase; an
  in-memory implementation serves tests/dev. See
  [provider-strategy.md](provider-strategy.md) and [ADR-006](adr/ADR-006-provider-strategy.md).
- **Database per service**: each stateful service owns its own database
  (`gdb_accounts_db`, `gdb_transactions_db`, …). No shared DB, no cross-service SQL.
- **Session-owning Unit of Work** ([ADR-004](adr/ADR-004-unit-of-work.md)): the
  provider hands out a UoW that owns the `AsyncSession`; repositories write
  through it and never commit; the use case commits once; DI closes the session.
- Schema authority is **EF Core Migrations** in production ([ADR-008](adr/ADR-008-database-migration-strategy.md));
  `create_all` is a dev convenience only.

## 6. Cross-cutting concerns (`gdb_common`)

The shared library (`libs/gdb_common`) provides the platform's cross-cutting
capabilities, wired into each `main.py`:

| Capability | Module | Wiring |
|---|---|---|
| Correlation ID + security headers + JSON logs | `observability` | `install_observability(app)` |
| W3C distributed tracing (`traceparent`, trace/span ids) | `observability` | (same) |
| Prometheus metrics + `/metrics` | `metrics` | `install_metrics(app)` |
| Safe exception envelope (no leakage) | `exceptions` | `install_exception_handlers(app)` |
| Circuit breaker | `resilience` | `CircuitBreaker(...)` in clients |
| Rate limiting | `ratelimit` | gateway |
| Service discovery | `discovery` | `registry_service` |
| JWT/RBAC + internal API key | `auth_dependencies`, `internal_auth`, `jwt_validation` | route deps |
| Domain/application base types | `domain`, `application` | imported by services |
| Composition helpers | `composition` | composition roots |
| CQRS buses | `cqrs` | transactions |

See [operations/observability.md](../operations/observability.md) for how logs,
metrics, and tracing work at runtime.

## 7. Quality gates

- **Test suites** per service (unit + API + integration), run on the in-memory provider.
- **Architecture gate** — enforces the dependency rule (§3).
- **OpenAPI gate** — every service's OpenAPI is diffed against a frozen snapshot
  in `openapi-snapshots/`; drift fails the gate ([ADR-009](adr/ADR-009-api-contract-stability.md)).

## Related

- [C4 diagrams](c4/README.md) · [Sequence diagrams](sequence-diagrams/README.md) · [ADRs](adr/README.md)
- [Request flow](request-flow.md) · [Service development guide](service-development-guide.md)



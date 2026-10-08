# GDB Enterprise Architecture Blueprint

**Status:** Approved target architecture (frozen). Every implementation phase MUST conform to this document and the ADRs in [`adr/`](adr/).
**Scope:** `gdb-service` (10 ASP.NET Core Web API microservices + React frontend + `libs/gdb_common`).
**Grounded in:** Phase 0 baseline — [phase-0-baseline-report](../enterprise-migration/phase-0-baseline-report.md), [api-route-inventory](../enterprise-migration/api-route-inventory.md), [provider-support-matrix](../enterprise-migration/provider-support-matrix.md), [database-schema-baseline](../enterprise-migration/database-schema-baseline.md), [backward-compatibility-contracts](../enterprise-migration/backward-compatibility-contracts.md), [migration-risk-register](../enterprise-migration/migration-risk-register.md).
**This document changes no code.** It defines *where the code is going*.

> **Reading key.** "Today" = the as-built Phase-0 state. "Target" = the approved end-state. "Δ" marks a gap the migration must close. Nothing here is implemented by writing this document.

---

## 1. Overall Architecture Overview

GDB stays a **microservices** platform; each service becomes an internally **Clean Architecture** application with four rings + a composition root:

```
API (interface)  →  Application (use cases)  →  Domain (enterprise rules)  ←  Infrastructure (adapters)
                                    ▲                                              │
                                    └──────────── Composition Root wires it all ───┘
```

- **Direction of dependencies always points inward** toward Domain (ADR-001). Domain depends on nothing external.
- **Infrastructure depends on Domain** (implements Domain-owned interfaces), never the reverse.
- Cross-cutting concerns (auth, logging, correlation, resilience, config) live in `libs/gdb_common` and are consumed at the edges (API / Infrastructure), never inside Domain.
- Persistence is pluggable via the **Provider Strategy** (5 providers) behind **Repository interfaces** + a **Unit of Work** (ADR-003, ADR-004, ADR-006).
- Services remain independently deployable, DB-per-service, communicating over synchronous HTTP today; an **event strategy** (§29) is defined for later async decoupling.

**Target style:** Clean Architecture + DDD-lite + Repository + Unit of Work + Explicit Mapping + Provider Strategy + Composition-Root DI, per microservice.

## 2. High-Level Component Diagram

```
                         ┌────────────────────────────┐
                         │      React Frontend :3000   │
                         └───────────────┬────────────┘
                                         │ HTTPS (Bearer JWT)
                         ┌───────────────▼────────────┐
                         │  central_gateway_service    │  :8000  (reverse proxy, CORS, rate-limit)
                         │  StaticResolver (→ registry)│
                         └───┬───────┬───────┬─────────┘
        ┌────────────────────┘       │       └───────────────────────┐
        ▼                            ▼                                ▼
 ┌────────────┐   ┌──────────────┐   ┌────────────┐   ┌────────────┐   ┌────────────┐
 │  auth :8004│   │ users :8003  │   │accounts:8001│  │transactions│   │ aadhar:8005│
 │  (JWT/RBAC)│   │ (user mgmt)  │   │ (accounts) │   │   :8002    │   │ company:8006│
 └─────┬──────┘   └──────┬───────┘   └─────┬──────┘   └─────┬──────┘   │ notify:8007 │
       │ gdb_auth_db     │ gdb_users_db    │ gdb_accts_db   │ gdb_txns  │ payment:8008│
       │                 │                 │                │           └────────────┘
       └─────────────────┴───────── libs/gdb_common ────────┴─── (auth, obs, resilience, cqrs, discovery)
                                         │
                          registry_service :8010 (service discovery — built, static today)
```
Money movement (representative): `transactions → accounts (debit/credit, circuit-breaker) → notification`. Account opening: `accounts → aadhar / company → notification`. Login: `auth → users`.

## 3. Microservice Responsibilities

| Service | Port | Owns (DB) | Core responsibility | Target domain aggregates |
|---|---|---|---|---|
| auth_service | 8004 | gdb_auth_db | Issue/verify/revoke JWT, throttle logins | `AuthToken`, `AuthAudit` |
| users_service | 8003 | gdb_users_db | User CRUD, roles, credential verify | `User` |
| accounts_service | 8001 | gdb_accounts_db | Savings/Current accounts, balance, PIN, Aadhaar encryption | `Account` (+ `Savings`/`Current`), `Money` VO |
| transactions_service | 8002 | gdb_transactions_db | Deposits/withdrawals/transfers, limits, idempotency, logs | `Transfer`, `TransactionLog`, `TransferLimit`, `IdempotencyKey` |
| aadhar_service | 8005 | — (stateless) | Simulated Aadhaar verification | none (stateless) |
| company_crv_service | 8006 | — (stateless) | Simulated company registration verify | none |
| notification_service | 8007 | JSON file | Send/list notifications | none (file store) |
| central_payment_gateway_service | 8008 | — | Simulated payment process/validate | none |
| central_gateway_service | 8000 | — | API Gateway (reverse proxy, CORS, rate-limit) | none |
| registry_service | 8010 | in-memory | Service discovery registry (static today) | none |

**Rule:** a service never reaches into another service's database. Cross-service data is obtained via that service's API (internal endpoints + `X-Internal-API-Key`).

## 4. Folder Structure (target, per stateful service — additive, no moves)

Existing folders stay where they are (STRICT RULE: no folder moves). The target introduces `domain/`, `application/`, `mappers/`, and `unit_of_work.py` **alongside** current folders, and re-homes logic over successive phases:

```
<service>/app/
  api/                 # interface layer: routers, request/response models binding, DI wiring points
  application/         # use cases / services orchestrating domain (target home for *_service.py logic)
    services/          # application services (use-case orchestrators)
    cqrs/              # commands/queries/buses (transactions today; optional elsewhere)
  domain/              # NEW: entities, value objects, aggregates, domain services, repository INTERFACES, domain exceptions
    entities/
    value_objects/
    repositories/      # repository interfaces (ABCs) — owned by domain
    services/          # domain services (pure rules spanning entities)
  infrastructure/      # adapters implementing domain interfaces
    repositories/      # Entity Framework Core / InMemory / asyncpg impls (today under app/repositories/)
    database/          # providers/, orm_models.py, connection, EF Core Migrations
    integration/       # httpx clients to peers
    mappers/           # NEW: DTO↔Domain↔ORM mapping
  models/              # DTOs (C# DTOs) — request/response contracts
  dependencies/        # composition root: providers.py (get_* factories), internal_auth
  config/              # settings.py (C# DTOs BaseSettings)
  unit_of_work.py      # NEW: UoW abstraction + per-provider implementations
  main.py              # app assembly + lifespan
```

> **Migration note:** because folders may not be moved in this phase, the target uses **new** `domain/`, `application/`, `mappers/` packages and leaves today's `services/`, `repositories/`, `database/` in place; logic is relocated incrementally per vertical slice. The *conceptual* layer of each existing folder is mapped in §5.

## 5. Clean Architecture Layers (with layer responsibilities — Task 6)

| Layer | Today's folders | Responsibilities | Allowed deps | Forbidden deps | Example |
|---|---|---|---|---|---|
| **API (Interface)** | `api/`, `main.py`, `models/` (DTOs) | HTTP routing, auth/authz dependency wiring, DTO (de)serialization, status/error mapping, OpenAPI | Application, DTOs, `gdb_common` auth/obs | Domain internals, ORM, repositories, SQL | `account_routes.create_savings_account` calls an application service, returns `AccountResponse` |
| **Application (Use Cases)** | `services/`, `cqrs/` | Orchestrate a use case: validate intent, load aggregates via repo interfaces, invoke domain, commit via UoW, call integration ports | Domain, repository **interfaces**, UoW interface, DTOs (in/out), mapper interfaces | ORM classes, Entity Framework Core/asyncpg, httpx concretes, ASP.NET Core Web API `Request` | `AccountApplicationService.open_savings(dto) -> AccountResponse` |
| **Domain (Enterprise Rules)** | *(new)* `domain/` | Entities, value objects, aggregates, invariants, domain services, repository **interfaces**, domain exceptions | **Nothing external** (pure C# + stdlib) | ASP.NET Core Web API, C# DTOs, Entity Framework Core, httpx, settings, logging frameworks | `Account.debit(Money)` raises `InsufficientFundsError` |
| **Infrastructure (Adapters)** | `repositories/`, `database/`, `integration/`, *(new)* `mappers/` | Implement domain interfaces: repositories, UoW, providers, ORM, mappers, integration clients, encryption | Domain (to implement its interfaces), `gdb_common`, DB drivers, httpx | Application/API (must not import upward) | `Entity Framework CoreAccountRepository(AccountRepository)` |
| **Composition Root** | `dependencies/providers.py`, `main.py` | The only place that knows concretes: build provider → repos → UoW → mappers → app services; bind to ASP.NET Core Web API `Depends` | Everything (it wires everything) | Being imported *by* Domain/Application (they receive built objects, never import the root) | `get_account_service()` assembles the graph |

**Golden rule:** an import may only point **inward or sideways within its ring**, never outward. Domain has zero outward imports.

## 6. Dependency Direction Rules

```
API ─────────────▶ Application ─────────────▶ Domain ◀───────────── Infrastructure
                                                 ▲                          │
                                                 └──── implements ──────────┘
Composition Root ─▶ (constructs concretes and injects them everywhere)
```
1. **Domain depends on nothing.** No framework, no ORM, no settings, no logging library (ADR-001).
2. **Application depends only on Domain** (entities + repository/UoW **interfaces** + DTOs). It never imports Infrastructure concretes.
3. **Infrastructure depends on Domain** — it *implements* the interfaces Domain declares (Dependency Inversion). This is why "infrastructure depends on domain."
4. **API depends on Application** (+ DTOs + `gdb_common` edge concerns). It never touches ORM or repositories directly.
5. **Composition Root** is the single place allowed to import concretes from every layer and wire them.
6. Enforced by convention + review + (recommended) an import-linter contract (§30). **No new library is added by this document.**

## 7. Request Flow

```
HTTP request
  → API router (ASP.NET Core Web API @router.*)                         [api/]
  → Depends: JWT validate + RBAC (gdb_common) + internal-key (if internal)
  → bind & validate DTO (C# DTOs request model)           [models/]
  → Application service method(dto)                         [application/services]
      → Mapper: DTO → Domain command/params                [infrastructure/mappers]
      → UnitOfWork.begin()                                  [unit_of_work]
      → repo.get(...) via repository INTERFACE → Domain entity/aggregate
      → Domain method enforces invariants (pure)            [domain/]
      → integration port call if needed (aadhar/accounts)   [infrastructure/integration]
      → repo.add/update(entity)                             [infrastructure/repositories]
      → UnitOfWork.commit()  (or rollback on error)
      → Mapper: Domain → response DTO
  → API returns response DTO + HTTP status                  [api/]
```

## 8. Response Flow

```
Domain entity/aggregate (post-op state)
  → Mapper.to_response_dto(entity)                          [infrastructure/mappers]
  → Application returns DTO to API
  → API sets response_model + status_code (unchanged contract, see backward-compat doc)
  → gdb_common middleware stamps X-Correlation-ID, security headers, JSON log line
  → serialized JSON to client
Errors: Domain/Application raise typed exceptions → API exception layer (§20) maps to
        the frozen error envelope { error_code, message, ... } — never leaks str(exc).
```

## 9. DTO Lifecycle

```
inbound:  JSON → C# DTOs request DTO (validate shape/format) → Mapper → domain input
outbound: domain entity → Mapper → C# DTOs response DTO → JSON
```
- DTOs live in `models/` and are the **only** shapes crossing the API boundary. They are **frozen contracts** (backward-compat doc).
- DTOs carry **no business logic** and are **never persisted**. A DTO never becomes an ORM row directly (ADR-005).
- Request DTO validation = *format* validation (types, regex, ranges). *Business* validation happens in Domain (§21).

## 10. Domain Entity Lifecycle

```
create:   Mapper(DTO) → Entity factory (e.g. Account.open(...)) → invariants checked in-constructor
load:     Repository.get(id) → Mapper(ORM row) → Entity (reconstituted, invariants assumed valid)
mutate:   Entity.method() enforces rules (e.g. Account.debit(Money)) → raises domain error on violation
persist:  Application → Repository.add/update(Entity) → Mapper(Entity) → ORM → UoW.commit()
discard:  on rollback the Entity is dropped; no partial persistence
```
- Entities are **pure C#** (dataclasses or plain classes), no C# DTOs/ORM base. Identity = domain key (e.g. `account_number`).
- Value Objects (`Money`, `Aadhaar`, `AccountNumber`) are immutable and self-validating (ADR-002).

## 11. ORM Lifecycle

```
ORM classes (Entity Framework Core 2.0 Mapped) live ONLY in infrastructure/database/orm_models.py
  → created by a Mapper from a Domain entity (write)
  → read from DB inside a Repository, then Mapper → Domain entity (never returned upward)
  → their session/transaction is owned by the Unit of Work
```
- **ORM models never leave Infrastructure** and are never returned to Application/API (ADR-005).
- Schema changes flow through **EF Core Migrations** (ADR-008), not runtime `create_all` in production.
- For `transactions_service` the "ORM" on the postgres/supabase path is the raw-asyncpg + `transactions_schema.sql` layer today (Δ — reconciled under ADR-004/§13 conflict C-1).

## 12. Repository Flow

```
Application → Repository INTERFACE (domain-owned ABC)
                 └─ implemented by → Entity Framework CoreXRepository | InMemoryXRepository | AsyncpgXRepository (infra)
                        └─ uses the UoW-provided session/connection
                        └─ Mapper converts ORM ↔ Domain at the boundary
```
- Interfaces live in `domain/repositories/`; implementations in `infrastructure/repositories/` (ADR-003).
- Repositories speak **Domain entities**, not dicts/ORM rows (Δ — today they return dicts; target returns entities).
- Provider-independence: the same interface is satisfied by every provider (ADR-006).

## 13. Unit of Work Flow

```
Application service:
    async with uow:                       # uow = injected UnitOfWork (per-request)
        acct = await uow.accounts.get(n)  # repositories exposed BY the uow, sharing one session/txn
        acct.debit(Money(amt))
        await uow.accounts.update(acct)
        await uow.logs.add(TransactionLog(...))
        await uow.commit()                # single atomic boundary
    # on exception → uow.rollback() automatically (context manager __aexit__)
```
- The UoW **owns the transaction boundary**: `begin` on enter, `commit` explicit, `rollback` on any exception (ADR-004).
- All repositories used in one use case are obtained **from the same UoW** so they share one session/connection → atomicity.
- **Ownership:** the Application layer owns *when* to commit; the UoW owns *how*. Repositories never commit on their own (Δ — today each repo method commits independently).
- Providers back the UoW differently: Entity Framework Core → `async_sessionmaker` session + `session.begin()`; asyncpg → pool connection + `connection.transaction()`; InMemory → a staging buffer flushed on commit.

## 14. Provider Flow

```
DATABASE_PROVIDER (env)
   → Settings.DATABASE_PROVIDER            [config/settings.py]
   → create_provider(provider)             [infrastructure/database/providers/factory.py]
   → DatabaseProvider concrete             [Entity Framework CoreProvider | InMemoryProvider | AsyncpgProvider]
   → engine/pool + session factory
   → provides repositories + UnitOfWork to the Composition Root
```
See §22 and ADR-006 for the full strategy. The factory is the **only** switch on provider name.

## 15. Dependency Injection Flow

```
ASP.NET Core Dependency Injection(get_account_service)          [api/]
   → get_account_service()                    [dependencies/providers.py = composition root]
        → provider = get_provider()           (singleton per process)
        → uow = provider.unit_of_work()       (per request)
        → mapper = AccountMapper()
        → return AccountApplicationService(uow=uow, mapper=mapper, integration=...)
   → injected into the route handler
```
- DI is **constructor injection** assembled at the composition root (ADR-007). No service self-resolves its dependencies (Δ — today `AccountService.__init__` calls `get_provider()`; target injects it).
- No module-level mutable service singletons in the request path (Δ — today `deposit_service = DepositService()`).

## 16. Configuration Flow

```
.env / environment → C# DTOs Settings(BaseSettings) per service   [config/settings.py]
   → module-level `settings` singleton
   → _assert_secure_config() fail-fast in production
   → consumed by Composition Root + Infrastructure (never by Domain)
```
- Domain and Application receive **values**, never the `settings` object.
- Secrets are validated by `_assert_secure_config()` (extend to cover `PIN_ENCRYPTION_KEY` and users `SECRET_KEY` — Δ, R-10). Target: external secrets manager (§28/§27, ADR-006 note). **No secret values in docs or commits** (R-01).

## 17. Authentication Flow

```
Client → auth_service POST /api/v1/auth/login (login_id, password)
   → verify vs users_service (internal) → issue JWT (RS256 if JWT_PRIVATE_KEY else HS256) with sub/login_id/role/jti/exp
Client → any service with `Authorization: Bearer <JWT>`
   → gdb_common JWTValidator.validate_token (RS256 then HS256) → claims
   → Depends(get_current_user) provides the principal to the API layer
Revocation: jti tracked in auth_tokens (is_revoked).
```
Auth is an **edge concern** (API + `gdb_common`); Domain never sees a token (ADR-001).

## 18. Authorization Flow

```
API route declares role requirement:
   Depends(require_admin() | require_admin_or_teller() | require_admin_or_teller_or_manager() | require_manager_or_teller_dependency | get_current_user)
   → gdb_common checks claim.role against the allowed set (ENVIRONMENT-aware dev bypass, prod strict)
Owner checks (e.g. users view-self, verify-pin) are enforced in-handler / application layer.
Internal endpoints: Depends(verify_internal_api_key) at router level (X-Internal-API-Key, constant-time compare).
```
Roles: `ADMIN`, `TELLER`, `MANAGER`. The route→role matrix is frozen in the route inventory and must not change silently (ADR-009).

## 19. Logging Flow

```
gdb_common.install_observability(app):
   middleware: read/mint X-Correlation-ID → correlation_id_ctx (ContextVar) → JsonFormatter stamps every log
   security headers added on response
Application/Domain: log via stdlib logging (structured); Domain logging kept minimal and side-effect-free.
LOG_FORMAT=json enables JSON logs. Target: add OpenTelemetry trace/span IDs (§27, Δ — not implemented today).
```
Logs never contain secrets, PINs, full Aadhaar, or tokens (masking preserved, R-10).

## 20. Exception Flow

```
Domain raises typed domain errors (e.g. InsufficientFundsError(DomainError))
Application catches/ō translates to application errors where useful
API exception layer (target: one shared gdb_common installer) maps:
   DomainError/AppError → { "error_code", "message", "status": "error" } + mapped HTTP code
   unhandled Exception   → 500 generic { "error_code":"INTERNAL_ERROR", "message":"Internal server error" }
NEVER return str(exc) to clients (Δ — aadhar/company do today, R-07).
```
Target error envelope is standardized across all services (today it varies — see backward-compat doc; changes must stay additive/compatible, ADR-009).

## 21. Validation Flow

```
Format validation  → API/DTO layer (C# DTOs: types, regex, length, ranges)   e.g. login_id ^[a-zA-Z0-9._-]+$
Business validation → Domain (invariants on entities/VOs)                       e.g. balance >= 0, transfer within limit
DB constraints      → last-resort safety net (unique, FK) — NOT the primary rule location
```
**Rule:** invariants live in Domain, not in DB CHECK constraints (Δ — transactions relies on postgres-only CHECKs, R-03; conflict C-2). `transfer_mode=CHEQUE` DTO-vs-DB mismatch resolved by making Domain the source of allowed modes.

## 22. Database Provider Strategy

Five providers, selected by `DATABASE_PROVIDER`, behind one factory (ADR-006):

| Provider | Backing | Driver | Use case |
|---|---|---|---|
| `inmemory` | dict store | — | tests, local, CI |
| `sqlite` | file | aiosqlite | local dev, fast integration tests |
| `mysql` | server | aiomysql | on-prem / classic deployments |
| `postgres` | server | asyncpg (Entity Framework Core for accounts/users/auth; raw asyncpg for transactions) | primary target DB |
| `supabase` | hosted postgres | asyncpg + SSL | managed cloud |

- **pgAdmin is NOT a provider** — it is a GUI administration client for PostgreSQL/Supabase. There is no `pgadmin` value and no `PgAdminProvider` (ADR-006).
- Target: unify transactions onto the Entity Framework Core path OR wrap its asyncpg path in the same UoW/repository interfaces so provider choice never leaks upward (Δ — conflict C-1).
- Adding a new provider changes **only Infrastructure + Composition Root** (§33, Task 7).

## 23. CQRS Usage

- **Where:** `transactions_service` (`app/cqrs/` + `gdb_common/cqrs.py`) — Commands `Deposit/Withdraw/Transfer`, Queries `GetTransferLimit/GetAllTransferRules`, a `CommandBus`/`QueryBus`.
- **Target policy:** CQRS is **optional and localized**. Use it where a service has many write use cases with cross-cutting handling (transactions). Do **not** impose buses on simple CRUD services (accounts/users/auth keep plain application services). Single write model (no separate read store) unless/until read scaling demands it.
- Commands/queries are Application-layer messages; handlers orchestrate Domain via UoW.

## 24. Circuit Breaker Usage

- **Where today:** exactly one call site — `transactions → accounts` (`gdb_common/resilience.py::CircuitBreaker`, `failure_threshold=5`, `reset_timeout=30s`).
- **Target policy:** wrap **every synchronous cross-service call that is on a critical path** (transactions→payment, transactions→notification, accounts→aadhar/company) in a breaker. Breakers live in Infrastructure integration clients; Application sees a port that may raise a typed `DependencyUnavailable` error. Open-circuit → mapped to 503 with the standard envelope.

## 25. Retry Policy

- **Today:** **Not implemented** (no retry/backoff anywhere).
- **Target:** idempotent, read-only, or safely-retryable outbound calls get **bounded exponential backoff with jitter** (e.g. 3 attempts) implemented in Infrastructure integration clients, *inside* the circuit breaker (retry first, breaker counts final failures). **Never** auto-retry non-idempotent money mutations without an idempotency key (transactions already carries `Idempotency-Key`). Retry config is per-client, injected from settings. (Library choice deferred to the implementation phase; none added by this document.)

## 26. Rate Limiting

- **Today:** gateway token-bucket, **off by default** (`GATEWAY_RATE_LIMIT_PER_MIN=0`), in-process (`gdb_common/ratelimit.py`).
- **Target:** enable at the gateway with a sane default; move to **distributed** limiting (Redis-backed, §28) so it works across replicas. Per-principal + per-IP limits; auth/login gets a stricter bucket. Limiter stays an **edge concern** (gateway/API), never in Domain.

## 27. Observability

- **Today:** JSON logs + `X-Correlation-ID` propagation only. **No tracing/metrics** (no OTEL env/vars).
- **Target:**
  - **Tracing:** OpenTelemetry spans per request + per outbound call; correlation-id ↔ trace-id linkage.
  - **Metrics:** Prometheus counters/histograms (request rate, latency, error rate, breaker state, DB pool) + `/metrics`.
  - **Logs:** keep structured JSON; add trace/span IDs; enforce no-secret redaction.
  - **Health:** keep `/health` `/ready` `/live`; `ready` reflects DB + critical deps.
  (Instrumentation libraries are chosen in the implementation phase; none added here.)

## 28. Redis Usage

- **Today:** **Not implemented** (no Redis anywhere).
- **Target (single shared responsibility list, introduced in a later phase):**
  1. **Distributed rate limiting** (§26) — shared token buckets across gateway replicas.
  2. **Caching** — hot read-through cache for reference data (transfer-limit rules, account privilege lookups) with explicit TTL + invalidation.
  3. **Shared idempotency / short-lived coordination** — cross-replica idempotency reservations and login-throttle counters (today in-process).
- Redis is **Infrastructure** only. Domain/Application depend on a `Cache` / `RateLimitStore` **port**; Redis is one adapter (InMemory adapter used in tests). No Redis dependency is added by this document.

## 29. Event Strategy

- **Today:** **Not implemented** — all inter-service calls are synchronous HTTP; no broker, no domain events, no outbox (grep-confirmed).
- **Target (phased, later than the first vertical slices):**
  - **Domain Events** first (in-process): entities raise events (`AccountOpened`, `FundsTransferred`); Application dispatches them after `UoW.commit()`.
  - **Transactional Outbox** for reliability: events persisted in the same UoW transaction, relayed by a publisher.
  - **Broker** (Kafka/RabbitMQ) to move notifications/logging/analytics **off the synchronous path**.
  - **Saga** for multi-step money movement (deposit/transfer) replacing ad-hoc idempotency+compensation (R-04).
- Until then, synchronous calls + idempotency + circuit breaker remain the approved mechanism. Event infrastructure is a **port** in Domain, adapters in Infrastructure.

## 30. Coding Standards (Task 5)

- **C# style:** PEP 8; format with the project's existing formatter/style; max line length per repo convention; f-strings; no wildcard imports; module/function ordering stdlib → third-party → local.
- **Docstrings:** Google-style docstrings on every public module, class, and function — one-line summary + `Args:`/`Returns:`/`Raises:` (matches the existing `gdb_common` style). Domain methods document invariants they enforce.
- **Typing:** full type hints on all public signatures; `Mapped[...]` for ORM; `from __future__ import annotations` where helpful; no bare `Any` in Domain; Value Objects are typed and immutable (`frozen=True`/`slots`).
- **Async usage:** `async def` end-to-end (routes, application, repositories, integration); never block the event loop — offload CPU-bound work (bcrypt) to a thread pool (Δ, R-14); no `requests`/`time.sleep` in async paths; one session/connection per UoW.
- **Dependency Injection:** constructor injection only; no service resolves its own deps; the composition root (`dependencies/providers.py`) is the only place importing concretes (ADR-007).
- **Exception handling:** raise typed domain/application exceptions; never `except: pass`; never return `str(exc)` to clients; one shared exception-handler installer per service (§20).
- **Logging:** stdlib `logging` via `gdb_common` config; structured JSON; always within correlation context; never log secrets/PINs/full Aadhaar/tokens.
- **Testing:** see §32 — every use case has a unit test on the domain + an application test with an InMemory UoW; contract/snapshot tests guard API compatibility.
- **Naming:** see §31.
- **Folder structure:** conform to §4; new code goes in `domain/`/`application/`/`infrastructure/`/`mappers/`; no folder moves this phase.
- **Import discipline (recommended, not added as a dependency here):** an import-linter contract enforcing §6 (Domain imports nothing outward). Until automated, enforced in review.

## 31. Naming Conventions (Task 5 cont.)

| Thing | Convention | Example |
|---|---|---|
| Modules/packages | `snake_case` | `account_service.py`, `value_objects/` |
| Classes | `PascalCase` | `AccountApplicationService`, `Money` |
| Domain entities | noun, no suffix | `Account`, `Transfer` |
| Value objects | noun, no suffix | `Money`, `Aadhaar`, `AccountNumber` |
| Repository interface | `<Aggregate>Repository` (in `domain/repositories`) | `AccountRepository` |
| Repository impl | `<Provider><Aggregate>Repository` | `Entity Framework CoreAccountRepository`, `InMemoryAccountRepository` |
| Unit of Work | `UnitOfWork` (interface), `<Provider>UnitOfWork` | `Entity Framework CoreUnitOfWork` |
| Mapper | `<Aggregate>Mapper` | `AccountMapper` |
| DTOs | `<Name>Request` / `<Name>Response` | `SavingsAccountCreate`, `AccountResponse` |
| ORM models | `<Name>ORM`, `__tablename__` snake plural | `AccountORM` → `accounts` |
| Application services | `<Aggregate>ApplicationService` (or keep `<X>Service` where established) | `TransferApplicationService` |
| DI providers | `get_<thing>` | `get_account_service` |
| Domain exceptions | `<Reason>Error` | `InsufficientFundsError` |
| Env vars | `UPPER_SNAKE_CASE` | `DATABASE_PROVIDER` |

Existing public names that are **frozen contracts** (routes, DTO field names, table/column names, env vars) keep their current spelling regardless of the above (ADR-009).

## 32. Testing Strategy (Task 5 cont.)

- **Test pyramid per service:** many **domain unit tests** (pure, no I/O) → **application tests** (use case with **InMemory UoW/repos**) → fewer **API tests** (TestClient) → a thin **contract/snapshot** layer (OpenAPI + error-envelope) → optional **cross-provider** tests for critical invariants.
- **Provider matrix:** run each stateful service's suite under `inmemory` (always) and at least `sqlite` (CI); periodically against a live `postgres` for the money-movement paths (Δ — today only accounts sqlite is exercised).
- **Backward-compat gate:** an OpenAPI snapshot per service must be unchanged (except intended additions) — protects the frozen contracts (ADR-009).
- **Coverage:** introduce coverage tooling + a CI threshold (Δ — none today, R-05); install `dotnet test gdb-service-dotnet.slnx` into every service venv so all suites (incl. notification) run in their own environment.
- **No test modified in Phase 0/this phase.** New tests are added only in implementation phases.

## 33. Future Extensibility (Task 7)

**Adding a new database provider (e.g. MongoDB, a cloud SQL, Redis-as-store) changes only Infrastructure + Composition Root:**

```
1. Add a provider value handling in factory.create_provider()      [infrastructure/database/providers/factory.py]
2. Implement DatabaseProvider for it                               [infrastructure/database/providers/<new>_provider.py]
3. Implement each <Aggregate>Repository interface for it           [infrastructure/repositories/<new>_repositories.py]
4. Implement UnitOfWork for it                                     [infrastructure/... unit_of_work]
5. Add a Mapper if the storage shape differs                       [infrastructure/mappers/]
6. Register it in the Composition Root wiring                      [dependencies/providers.py]
```

**Unchanged by a new provider:** `api/` (routes/DTOs), `application/` (use cases), `domain/` (entities, VOs, **repository interfaces**), and the public API contract. This is the payoff of Dependency Inversion (ADR-001) + Repository (ADR-003) + Provider Strategy (ADR-006) + UoW (ADR-004).

Other extension points that follow the same "edges change, core doesn't" rule:
- **New transport** (gRPC, message consumer) → new API/adapter, same Application/Domain.
- **New cache/broker/observability backend** → new Infrastructure adapter behind an existing port.
- **New microservice** → same layered template; register in gateway + (future) discovery.

---

## Appendix A — Architecture Diagrams (Task 4)

### A.1 Full request pipeline
```
Client
  ↓
API                (ASP.NET Core Web API router, auth/authz deps)              [api/]
  ↓
DTO                (C# DTOs request model — frozen contract)      [models/]
  ↓
Mapper             (DTO → domain input)                            [infrastructure/mappers]
  ↓
Application        (use-case service / CQRS handler)               [application/]
  ↓
Domain             (entities, value objects, invariants)           [domain/]
  ↓
Repository Interface   (domain-owned ABC)                          [domain/repositories]
  ↓
Unit Of Work       (transaction boundary: begin/commit/rollback)   [unit_of_work.py]
  ↓
Repository Implementation  (Entity Framework Core | InMemory | Asyncpg)       [infrastructure/repositories]
  ↓
ORM                (Entity Framework Core Mapped models / asyncpg SQL)        [infrastructure/database/orm_models.py]
  ↓
Database           (inmemory | sqlite | mysql | postgres | supabase)
```

### A.2 Provider selection
```
DATABASE_PROVIDER (env)
  ↓
Provider Factory   create_provider()   [infrastructure/database/providers/factory.py]
  ↓
  ├─ "inmemory"  → InMemoryProvider   → InMemory repositories + InMemory UoW
  ├─ "sqlite"    → Entity Framework CoreProvider → sqlite+aiosqlite   engine/session
  ├─ "supabase"  → Entity Framework Core/Asyncpg → postgres + SSL     (asyncpg for transactions)
  ├─ "mysql"     → Entity Framework CoreProvider → mysql+aiomysql     engine/session
  └─ "postgres"  → Entity Framework Core/Asyncpg → postgresql+asyncpg engine/session (raw asyncpg for transactions)

  (pgAdmin is a PostgreSQL admin GUI — NOT a provider, no branch here)
```

### A.3 Layered dependency (inward-only)
```
        ┌────────────────────────── Composition Root ──────────────────────────┐
        │  (dependencies/providers.py, main.py — wires concretes, injects them) │
        └───────────────┬───────────────────────────────────┬──────────────────┘
                        │ builds                             │ builds
   ┌────────┐      ┌────▼─────┐      ┌────────┐      ┌────────▼───────────┐
   │  API   │ ───▶ │Application│ ───▶ │ Domain │ ◀─── │  Infrastructure    │
   │(routes)│      │(use cases)│      │(pure)  │impl  │(repos, UoW, ORM,   │
   └────────┘      └──────────┘      └────────┘      │ mappers, providers)│
        imports point inward ─────────────▶          └────────────────────┘
        Domain imports nothing outward.  Infrastructure implements Domain interfaces.
```

---

## Appendix B — Conformance checklist (per vertical slice)
- [ ] Domain has no outward imports (framework/ORM/settings free).
- [ ] Repository interface in `domain/`, impls in `infrastructure/`, returns entities.
- [ ] One UoW owns the transaction; repos don't self-commit.
- [ ] DTO↔Domain↔ORM via explicit Mapper; no ORM leaves Infrastructure.
- [ ] Composition root wires it; no self-resolution, no request-path singletons.
- [ ] Public routes/DTOs/status/error-envelope unchanged (OpenAPI snapshot green).
- [ ] Schema change (if any) via EF Core Migrations, not runtime create_all.
- [ ] Tests: domain unit + application (InMemory UoW) + contract snapshot pass.



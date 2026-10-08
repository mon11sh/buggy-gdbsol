# GDB Enterprise Microservice Architecture Standard

**Status:** MANDATORY. Every existing and future GDB microservice MUST conform. No service may violate it.
**Scope:** the reusable architecture template — **no business logic, no migration** is performed by this document.
**Foundations already shipped (Phase 2, `libs/gdb_common`):** `domain` (Entity, AggregateRoot, ValueObject, DomainEvent, DomainError, Specification, Result), `application` (UseCase, Command/Query, Mapper, DtoMapper, Clock, IdGenerator, Repository, IUnitOfWork), `composition` (ProviderRegistry, CompositionRoot). This standard tells every service how to use them.
**Companion docs:** [request-flow](request-flow.md) · [repository-strategy](repository-strategy.md) · [provider-strategy](provider-strategy.md) · [mapping-strategy](mapping-strategy.md) · [service-development-guide](service-development-guide.md) · [developer-checklist](developer-checklist.md) · [migration-template](migration-template.md). Grounded in the [enterprise-blueprint](enterprise-blueprint.md) + [ADRs](adr/) + Phase 0/1/2 reports + [business-capability-report](../enterprise-migration/business-capability-report.md).

---

## 1. Layers & Responsibilities

| Layer | Package | Responsibility | May depend on | MUST NOT depend on |
|---|---|---|---|---|
| **API** | `app/api/` | HTTP routing, auth/authz wiring (`Depends`), request/response DTO binding, status/error mapping, OpenAPI | Application, DTOs, `gdb_common` edge (auth/observability/exceptions) | Domain internals, ORM, repositories, SQL, HTTP clients |
| **Application** | `app/application/` | Orchestrate ONE use case: validate intent, load aggregates via repo interfaces, invoke domain, commit via UoW, call outbound **ports** | Domain, repository **interfaces**, **ports**, UoW interface, DTOs (in/out), mapper interfaces | ORM, Entity Framework Core, HTTP clients, ASP.NET Core Web API `Request`, provider concretes |
| **Domain** | `app/domain/` | Entities, aggregates, value objects, domain events, invariants, domain services, repository **interfaces**, domain exceptions | **stdlib + `gdb_common.domain` only** | ASP.NET Core Web API, Entity Framework Core, C# DTOs, httpx, settings, logging frameworks |
| **Infrastructure** | `app/infrastructure/` | Implement domain interfaces: ORM, repositories, providers, UoW, mappers, outbound adapters, HTTP clients, crypto | Domain (to implement its interfaces), `gdb_common`, DB drivers, httpx | API, Application (never import upward) |
| **Composition Root** | `app/composition/`, `app/api/dependencies/` | The ONLY place that knows concretes: load config, resolve provider, wire repositories/UoW/mappers/adapters, construct use cases, bind to `Depends` | Everything | Being imported by Domain/Application |

**Business-logic rule (mandatory):** business rules live **only** in Domain models (entities/aggregates/VOs/domain services). API, DTOs, repositories, ORM, infrastructure, and HTTP clients contain **zero** business rules.

---

## 2. Diagram — Request Flow
```
Client
  │  HTTP + Authorization: Bearer <JWT> (+ X-Internal-API-Key on internal routes)
  ▼
API Router (app/api/routers)                     Depends: JWT+RBAC / internal-key (gdb_common)
  ▼
Request DTO (C# DTOs, app/application/dto)        shape validation (types/regex/ranges)
  ▼
Request Validator (app/application/validators)     cross-field / format validation
  ▼
DTO → Domain Mapper (DtoMapper, infrastructure/mappers)   EXPLICIT
  ▼
Application Service / Use Case (app/application/use_cases)  orchestration only
  ▼
Domain Model (app/domain)                          BUSINESS LOGIC lives here
  ▼
Repository Interface (app/domain/repositories)     domain-termed port
  ▼
Unit of Work (app/application ← impl in infrastructure)  transaction boundary
  ▼
Repository Implementation (infrastructure/persistence/repositories)
  ▼
Domain → ORM Mapper (Mapper, infrastructure/mappers)      EXPLICIT
  ▼
ORM Models (infrastructure/persistence/orm)
  ▼
Database Provider (infrastructure/persistence/providers)  chosen in Composition Root
  ▼
Database
```

## 3. Diagram — Response Flow
```
Database
  ▼
ORM Model (infrastructure/persistence/orm)
  ▼
Repository Implementation                          reads rows within the UoW session
  ▼
Domain → (ORM→Domain) Mapper                        EXPLICIT: reconstitute aggregate
  ▼
Domain Model (app/domain)
  ▼
Response Mapper (DtoMapper.to_response)             EXPLICIT: domain → response DTO
  ▼
Response DTO (C# DTOs)                             frozen public contract (masking etc.)
  ▼
API Layer                                           sets response_model + status_code
  │  + gdb_common: X-Correlation-ID, security headers, safe error envelope
  ▼
Client
```

## 4. Diagram — Repository Flow
```
Application Use Case
  │ depends on ── Repository Interface (domain-owned ABC)  ── returns/accepts DOMAIN objects
  ▼
Unit of Work (owns the session/connection + transaction)
  ├── uow.<aggregate>  ─► Repository Implementation (infrastructure)
  │                          │ uses UoW-provided session (never opens its own)
  │                          ├─ Mapper: ORM ↔ Domain (explicit)
  │                          └─ ORM Models ─► Database Provider ─► Database
  └── uow.commit() / rollback()   (repositories NEVER commit themselves)

One interface  ─┬─ InMemoryRepository
                ├─ SQLiteRepository        (Entity Framework Core)
                ├─ MySQLRepository         (Entity Framework Core)
                ├─ PostgreSQLRepository    (Entity Framework Core / asyncpg)
                └─ SupabaseRepository      (Entity Framework Core + SSL)
```

## 5. Diagram — Provider Resolution Flow
```
DATABASE_PROVIDER (env)
  ▼
Settings.DATABASE_PROVIDER (app/config)
  ▼
Composition Root  ──►  ProviderRegistry (gdb_common.composition)
  │                        register("inmemory", factory)
  │                        register("sqlite",   factory)
  │                        register("mysql",    factory)
  │                        register("postgres", factory)
  │                        register("supabase", factory)
  ▼
registry.create(name)  ─►  DatabaseProvider  ─►  engine/pool + session factory
  ▼
Unit of Work factory  ─►  Repository Implementations (bound to the session)
  ▼
Application receives an IUnitOfWork — it NEVER learns which provider was chosen.
(pgAdmin is a PostgreSQL admin GUI, NOT a provider — no branch exists for it.)
```

## 6. Diagram — Microservice Communication Flow
```
Application Service / Use Case
  │  depends ONLY on the Port interface (never a transport type)
  ▼
Outbound Port (app/domain/ports  OR app/application/ports)   e.g. VerificationPort
  ▼
Infrastructure Adapter (infrastructure/adapters)             e.g. AadhaarAdapter / CompanyAdapter
  ▼
HTTP Client (infrastructure/clients)                         httpx + X-Internal-API-Key + X-Correlation-ID
  │  cross-cutting: circuit breaker (gdb_common.resilience), timeout, [retry — future]
  ▼
Target Microservice

Type variation is expressed by PORTS, not by branching in the use case:
  Savings  → VerificationPort → AadhaarAdapter → HTTP → Aadhaar Service (:8005)
  Current  → VerificationPort → CompanyAdapter → HTTP → Company CRV Service (:8006)
```

## 7. Diagram — Composition Root Flow
```
main.py (service entrypoint)
  ▼
CompositionRoot (subclass of gdb_common.composition.CompositionRoot)
  ├─ load_config()                       → Settings
  ├─ build ProviderRegistry              → register all 5 provider factories
  ├─ resolve provider_name()             → settings.DATABASE_PROVIDER
  ├─ create_unit_of_work()               → provider → session factory → UoW(+repos)
  ├─ build outbound adapters (ports)     → Aadhaar/Company/Notification/... adapters
  ├─ build mappers + Clock + IdGenerator
  └─ construct Use Cases (constructor injection)
         │
         ▼
   app/api/dependencies (get_<use_case>)  ─►  ASP.NET Core Dependency Injection  ─►  Router handlers
```

## 8. Diagram — Dependency Graph (inward-only)
```
        ┌──────────────────────── Composition Root ────────────────────────┐
        │      (config, provider selection, wiring, DI — knows concretes)   │
        └───────┬───────────────────────────────────────────────┬──────────┘
                │ builds                                          │ builds
   ┌────────┐   ▼          ┌────────────┐        ┌────────┐       ▼ ┌────────────────────┐
   │  API   │ ───────────► │Application │ ─────► │ Domain │ ◄────── │  Infrastructure    │
   │(routers│              │(use cases, │        │(pure:  │ impl.   │(orm, repos, UoW,   │
   │ + DTO) │              │ ports iface│        │ rules) │         │ mappers, adapters, │
   └────────┘              │ , mappers) │        └────────┘         │ clients, providers)│
        imports point INWARD ─────────────────►                    └────────────────────┘
   Domain imports nothing outward.  Infrastructure implements Domain interfaces.  No cycles.
```

---

## 9. Non-negotiable rules (summary)
1. Business logic **only** in Domain models. 2. Two **explicit** mapping layers (DTO↔Domain, Domain↔ORM); no reflection/implicit mapping. 3. Repository **interface per aggregate**, exposing only business methods; concrete impl per provider; no ORM/Session/dict leaks. 4. Provider selection **only** in the Composition Root; Application never knows the provider. 5. Cross-service calls **only** through a Port → Adapter → HTTP client; use cases never touch transport. 6. Constructor injection only; no service locator, no global lookup in business logic, no repo/provider creation in services or routes. 7. Dependencies point inward; Domain imports no framework; no cycles. 8. Public API/DTO/schema contracts are frozen (ADR-009) and guarded by the OpenAPI + architecture gates.

## 10. Enforcement
- **Architecture tests** (`libs/gdb_common/tests/test_architecture.py`, extended per service): domain imports no ASP.NET Core Web API/Entity Framework Core/C# DTOs; application imports no framework/infrastructure; routes never instantiate repositories/providers; composition root owns wiring.
- **OpenAPI compatibility gate** (`scripts/run_openapi_gate.py`): public contract unchanged.
- **Coding standards** ([../development/coding-standards.md](../development/coding-standards.md)) + **developer checklist** ([developer-checklist.md](developer-checklist.md)) in review.



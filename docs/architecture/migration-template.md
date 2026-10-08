# Migration Template — Standard Folder Structure, Dependencies, Communication, Tests

The single mandatory shape every GDB microservice adopts. A physical, copy-ready skeleton exists at [`service-template/`](../../service-template/). This document is the authoritative reference.

## Mandatory folder structure
```
<service>/
  app/
    api/
      routers/              # ASP.NET Core Web API routers (@router.*), thin — no business logic
      dependencies/         # DI providers (get_<use_case>) = composition-root bindings
    application/
      use_cases/            # UseCase subclasses (orchestration only)
      services/             # application services (coordination; optional)
      dto/                  # C# DTOs request/response DTOs (frozen contract)
      ports/                # outbound port interfaces (if not domain-owned)
      commands/             # command messages (writes)
      queries/              # query messages (reads)
      validators/           # request/format validators
    domain/
      entities/             # identity-bearing domain objects
      aggregates/           # aggregate roots (consistency boundaries)
      value_objects/        # immutable, self-validating values
      events/               # domain events
      repositories/         # repository INTERFACES (domain-owned)
      services/             # domain services (pure rules spanning entities)
      exceptions/           # domain exceptions (subclass gdb_common DomainError)
    infrastructure/
      adapters/             # Port adapters (e.g. AadhaarAdapter, CompanyAdapter)
      clients/              # httpx clients to peer services
      persistence/
        orm/                # Entity Framework Core ORM models (schema; no schema change here)
        repositories/       # repository IMPLEMENTATIONS (per provider)
        providers/          # provider wiring + Unit of Work implementations
      mappers/              # explicit DtoMapper + Mapper (DTO↔Domain↔ORM)
    composition/            # CompositionRoot (provider registry, wiring)
    config/                 # C# DTOs Settings + secure-config guard
  tests/
    api/                    # API/route tests (TestClient)
    application/            # use-case tests (InMemory UoW)
    domain/                 # domain + value-object tests
    repository/             # repository contract + implementation tests
    integration/            # cross-layer / provider integration tests
    architecture/           # per-service architecture tests (import rules)
```

## Mandatory dependency rules
```
ALLOWED:
  API           → Application
  Application   → Domain, Repository Interfaces, Ports
  Infrastructure→ Repository Interfaces, Ports, Domain
  Composition   → Everything

FORBIDDEN:
  Domain        → ASP.NET Core Web API | Entity Framework Core | C# DTOs | HTTP
  Application   → ORM | Entity Framework Core | HTTP Clients
  Repositories  → ASP.NET Core Web API | DTOs
  ORM           → Domain
  (no circular dependencies)
```

## Mandatory communication flow
```
Application → Outbound Port (interface) → Infrastructure Adapter → HTTP Client → Target Microservice
```
Application services never call HTTP clients directly and never know transport details; the only dependency is the Port interface. Per-type variation (savings→Aadhaar, current→Company) is expressed by which adapter implements the port, not by branching in the use case.

## Mandatory test structure
Every service SHALL contain: API tests, DTO tests, Mapper tests, Use-Case tests, Domain tests, Value-Object tests, Repository Contract tests, Repository Implementation tests, Provider tests, Unit-of-Work tests, Integration tests, Architecture tests, OpenAPI Compatibility tests.

## Reusing the Phase-2 foundation (`libs/gdb_common`)
| Need | Use |
|---|---|
| Entity / Aggregate / Value Object / Event / DomainError | `gdb_common.domain` |
| UseCase / Command / Query / Mapper / DtoMapper / Clock / IdGenerator / Repository / IUnitOfWork | `gdb_common.application` |
| ProviderRegistry / CompositionRoot | `gdb_common.composition` |
| Correlation id / security headers / safe exceptions | `gdb_common.install_observability`, `install_exception_handlers` |
| Circuit breaker / internal-key / JWT | `gdb_common.resilience`, `internal_auth`, `jwt_validation` |

## Migrating an EXISTING service (strangler, zero-downtime)
1. Add the new layers (`domain/`, `application/`, `infrastructure/`, `composition/`) **alongside** existing code — no folder moves.
2. Build the aggregate + interfaces + mapper + UoW + use cases against `gdb_common`, reusing the **existing ORM/schema**.
3. Prove the new stack with contract/integration tests (InMemory + sqlite).
4. Flip one route at a time to the new use case via `Depends`, keeping the DTO/status/response **identical** (OpenAPI gate green) — then delete the superseded service/repository code.
5. Never migrate money-movement/distributed flows before a Unit of Work + Saga story exists.



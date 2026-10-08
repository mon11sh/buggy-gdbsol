# Service Development Guide (Mandatory)

How to build (or migrate) a GDB microservice to the [enterprise standard](enterprise-microservice-standard.md). Reuses the Phase-2 `gdb_common` foundation — do not re-invent contracts.

## Step-by-step
1. **Scaffold the folder structure** (copy `service-template/` — see [migration-template](migration-template.md)). Same structure for every service.
2. **Model the Domain** (`app/domain/`): entities/aggregates subclassing `gdb_common.domain.AggregateRoot`; value objects subclassing `ValueObject`; domain events subclassing `DomainEvent`; domain exceptions subclassing `DomainError`. Put **all business rules here**. No framework imports.
3. **Declare repository interfaces** (`app/domain/repositories/`) per aggregate, extending `gdb_common.application.Repository`. Business methods only; domain types only.
4. **Declare outbound ports** (`app/domain/ports/` or `app/application/ports/`) for any cross-service dependency (e.g. `VerificationPort`, `NotificationPort`).
5. **Write use cases** (`app/application/use_cases/`) subclassing `gdb_common.application.UseCase`. Accept a Command/Query, orchestrate the domain, use the injected `IUnitOfWork` + ports, return a domain result. No I/O, no transport, no ORM.
6. **Define DTOs + validators** (`app/application/dto/`, `app/application/validators/`) — C# DTOs request/response models (frozen contract) + format validators.
7. **Implement Infrastructure** (`app/infrastructure/`):
   - `mappers/` — explicit `DtoMapper` (DTO↔Domain) and `Mapper` (Domain↔ORM).
   - `persistence/orm/` — Entity Framework Core models (reuse the existing schema; no schema change).
   - `persistence/repositories/` — one impl per provider satisfying the interface, using the UoW session + mapper.
   - `persistence/providers/` — provider wiring + `Entity Framework CoreUnitOfWork`/`InMemoryUnitOfWork`.
   - `adapters/` + `clients/` — Port adapters over httpx clients (internal key + correlation id; circuit breaker on critical paths).
8. **Wire the Composition Root** (`app/composition/`): subclass `gdb_common.composition.CompositionRoot`; register all 5 provider factories in a `ProviderRegistry`; build UoW, mappers, adapters, use cases; expose `get_<use_case>` in `app/api/dependencies/`.
9. **Bind routes** (`app/api/routers/`): handlers depend on the use-case providers via `Depends`; map request DTO → command, call the use case, map domain → response DTO. **No business logic, no repository/provider construction in routes.**
10. **Test** every layer (see [migration-template](migration-template.md) test structure) and keep the **OpenAPI** + **architecture** gates green.

## Golden rules
- Business logic ONLY in Domain. Application coordinates. Repositories persist. DTOs transport. ORM persists.
- Cross-service calls ONLY via Port → Adapter → HTTP client.
- Provider selection ONLY in the Composition Root.
- Explicit mapping only. Constructor injection only. Dependencies point inward. No cycles.
- Public API/DTO/schema contracts are frozen (ADR-009).



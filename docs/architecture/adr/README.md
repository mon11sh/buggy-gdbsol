# Architecture Decision Records (ADR) — GDB Enterprise Migration

These ADRs are **frozen decisions** that every implementation phase must follow. They complement the [enterprise-blueprint](../enterprise-blueprint.md) and are grounded in the Phase 0 baseline under [`../../enterprise-migration/`](../../enterprise-migration/).

| ADR | Decision | Status |
|---|---|---|
| [ADR-001](ADR-001-clean-architecture.md) | Adopt Clean Architecture (inward-only dependencies; domain depends on nothing) | Accepted |
| [ADR-002](ADR-002-domain-driven-design.md) | Use DDD-lite (entities, value objects, aggregates, domain services, repo interfaces) | Accepted |
| [ADR-003](ADR-003-repository-pattern.md) | Repository pattern with provider-independent Entity Framework Core/InMemory/asyncpg impls | Accepted |
| [ADR-004](ADR-004-unit-of-work.md) | Introduce a Unit of Work owning transaction boundaries (commit/rollback) | Accepted |
| [ADR-005](ADR-005-explicit-mapping.md) | Explicit DTO↔Domain↔ORM mapping; never expose ORM models | Accepted |
| [ADR-006](ADR-006-provider-strategy.md) | Provider strategy: inmemory/sqlite/mysql/postgres/supabase; pgAdmin is NOT a provider | Accepted |
| [ADR-007](ADR-007-composition-root.md) | Composition root + constructor injection; no self-resolution / request-path singletons | Accepted |
| [ADR-008](ADR-008-database-migration-strategy.md) | EF Core Migrations is authoritative; no automatic `create_all` in production | Accepted |
| [ADR-009](ADR-009-api-contract-stability.md) | API/inter-service contracts stay backward compatible throughout the migration | Accepted |
| [ADR-010](ADR-010-observability.md) | Observability as dependency-free `gdb_common` primitives: correlation, W3C tracing, Prometheus metrics | Accepted |

**Change process:** an ADR is superseded, never edited away — add a new ADR that references and supersedes it. Contract- or schema-affecting ADRs (008, 009) additionally require the snapshot gates described within them.



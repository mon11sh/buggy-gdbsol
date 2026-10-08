# ADR-001 — Adopt Clean Architecture

- **Status:** Accepted (frozen for the migration)
- **Date:** Phase 0.5 (architecture freeze)
- **Deciders:** Lead Software Architect, GDB Enterprise Migration
- **Related:** [enterprise-blueprint](../enterprise-blueprint.md) §5–6, ADR-002, ADR-003, ADR-004, ADR-005, ADR-007

## Context
Phase 0 found each GDB service internally layered (`api → services → repositories → database`) but with an **anemic domain** (business rules in `services/` + `utils/validators.py`), **no domain layer**, **no Unit of Work**, **no Mapper**, and at least one **Dependency-Inversion leak** (`AccountService.__init__` calls `get_provider()` directly). We need one architecture that keeps business rules independent of ASP.NET Core Web API, Entity Framework Core/asyncpg, and the chosen database, so persistence and transport can change without touching the core.

## Decision
Adopt **Clean Architecture** with four concentric rings + a composition root, applied per microservice:

```
API (interface) → Application (use cases) → Domain (enterprise rules) ← Infrastructure (adapters)
                                   ▲                                          │
                                   └────────── Composition Root wires ────────┘
```

### Dependency rules (the core of this ADR)
1. **Source-code dependencies point only inward.** Outer rings depend on inner rings; inner rings know nothing of outer rings.
2. **Domain depends on nothing external** — no ASP.NET Core Web API, no C# DTOs base classes, no Entity Framework Core, no httpx, no settings, no logging framework. pure C# + stdlib only.
3. **Application depends only on Domain** — entities, value objects, and **interfaces** (repository/UoW ports) that Domain declares.
4. **Infrastructure depends on Domain** — it *implements* the interfaces Domain owns.
5. **Composition Root** (`dependencies/providers.py`, `main.py`) is the single place allowed to import concretes from every ring and wire them together.

### Why infrastructure depends on domain (Dependency Inversion)
The Domain declares **what** it needs as an interface it owns (e.g. `AccountRepository`, `UnitOfWork`). Infrastructure provides the **how** (`Entity Framework CoreAccountRepository`, `AsyncpgUnitOfWork`) by *implementing* those interfaces. The arrow of dependency therefore runs **from Infrastructure into Domain**, not the other way. This is what lets us swap Entity Framework Core for asyncpg, or Postgres for MySQL, without editing a single line of Domain or Application code (see ADR-006, blueprint §33).

### Why domain never depends on frameworks
- **Testability:** domain rules run in microseconds with no DB/HTTP/event loop.
- **Longevity:** frameworks churn (ASP.NET Core Web API, Entity Framework Core, C# DTOs majors); business rules (min balance ≥ 0, transfer limits, Aadhaar rules) do not. Coupling them forces rewrites on every framework upgrade.
- **Substitutability:** the same domain backs 5 database providers and (future) new transports/brokers.
- **Clarity of ownership:** a rule lives in exactly one place (the entity/VO), not scattered across routes, services, validators, and DB CHECK constraints (a real Phase-0 problem — R-03).

## Consequences
**Positive:** framework-independent, fully testable core; provider/transport swaps are edge-only changes; a single conformance checklist (blueprint Appendix B).
**Negative / cost:** more indirection (mappers, interfaces, UoW); a learning curve; migration effort per vertical slice.
**Neutral:** existing folders are not moved this phase; new `domain/`/`application/`/`mappers/` packages are introduced alongside and logic is relocated incrementally (blueprint §4).

## Enforcement
Convention + code review now; a recommended import-linter contract later (no dependency added by the architecture docs). Domain packages must contain zero outward imports.

## Alternatives considered
- **Keep layered-anemic (status quo):** rejected — rules stay scattered, DB-coupled, and provider-divergent.
- **Hexagonal/Onion:** effectively equivalent; Clean Architecture chosen for its explicit ring/naming vocabulary the team can standardize on.
- **Full DDD with CQRS+ES everywhere:** rejected as over-engineering for CRUD services (see ADR-002 "DDD-lite", blueprint §23/§29).



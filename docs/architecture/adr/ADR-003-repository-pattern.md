# ADR-003 — Repository Pattern with Provider-Independent Implementations

- **Status:** Accepted
- **Related:** ADR-001, ADR-002, ADR-004, ADR-006, blueprint §9, §12, §22

## Context
Phase 0 confirmed GDB already uses repositories, but with two weaknesses: the accounts contract is a **13-method fat interface** (ISP smell), and repositories **return dicts/ORM rows** rather than domain entities, leaking persistence shape upward. transactions has three implementations per aggregate (asyncpg default + Entity Framework Core + InMemory); accounts/users/auth have Entity Framework Core + InMemory. We want one contract per aggregate, satisfied identically by every provider.

## Decision
Use the **Repository Pattern** with the interface owned by Domain and concrete implementations in Infrastructure.

### Repository interfaces (Domain-owned ports)
- Live in `domain/repositories/`, one per aggregate: `AccountRepository`, `TransferRepository`, `TransactionLogRepository`, `TransferLimitRepository`, `IdempotencyRepository`, `UserRepository`, `AuditRepository`, `AuthTokenRepository`, `AuthAuditRepository`.
- Expressed in **domain terms**: `get(id) -> Entity | None`, `add(entity)`, `update(entity)`, aggregate-specific queries. They accept and return **domain entities**, never dicts or ORM rows.
- Kept **cohesive** (favor per-aggregate methods; split the fat `AccountRepository` responsibilities where they represent different aggregates).

### Entity Framework Core implementations
- `Entity Framework Core<Aggregate>Repository` in `infrastructure/repositories/`, constructed with a **session/session-factory supplied by the Unit of Work** (ADR-004), not self-managed.
- Use Entity Framework Core 2.0 typed ORM (`Mapped`) from `infrastructure/database/orm_models.py`; convert ORM ↔ entity via an explicit **Mapper** (ADR-005). No `session.commit()` inside a repository method (the UoW commits).

### InMemory implementations
- `InMemory<Aggregate>Repository` over a dict store; the **reference implementation for tests and CI** and the `inmemory` provider. Must satisfy the identical interface and entity contract so tests exercise real use cases.

### asyncpg implementation (transactions)
- transactions' postgres/supabase path uses raw asyncpg today. Target: wrap it behind the same repository interface + a UoW-provided connection so provider choice never leaks (blueprint conflict C-1).

### Provider independence
- Application/Domain depend only on the **interface**; the concrete is chosen by the Provider Factory (ADR-006) and injected by the Composition Root (ADR-007). Swapping providers changes no interface and no caller.

## Consequences
**Positive:** callers are storage-agnostic; tests run fully in-memory; entities (not dicts) flow upward.
**Negative:** requires a mapper per aggregate and disciplined "no-commit-in-repo".
**Δ from today:** repos must be refactored to return entities and to receive their session from the UoW (implementation phases, not now).

## Alternatives considered
- **Active Record / ORM models as domain:** rejected — couples domain to Entity Framework Core, breaks ADR-001/005.
- **Generic single repository:** rejected — hides aggregate boundaries; per-aggregate repos keep consistency boundaries explicit.



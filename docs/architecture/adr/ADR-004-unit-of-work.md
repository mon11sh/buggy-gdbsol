# ADR-004 — Introduce a Unit of Work

- **Status:** Accepted
- **Related:** ADR-001, ADR-003, ADR-006, blueprint §13, migration-risk-register R-04

## Context
Phase 0 confirmed **no Unit of Work exists** (grep-confirmed): every repository method opens its own session and commits independently. Multi-step use cases — most importantly money movement in transactions (debit + credit + two logging legs) — have **no atomic boundary**; consistency is approximated with idempotency keys, a circuit breaker, and manual compensation (R-04). A partial failure can leave inconsistent balances/logs.

## Decision
Introduce a **Unit of Work (UoW)** that owns the transaction boundary for a use case and exposes the repositories that participate in it.

### Transaction boundaries
- The UoW is an **async context manager**, created **per request/use case** and injected by the Composition Root.
- Entering the context begins a transaction; all repositories obtained from the UoW share **one** session/connection.
```python
async with uow:
    acct = await uow.accounts.get(n)
    acct.debit(Money(amount))
    await uow.accounts.update(acct)
    await uow.logs.add(TransactionLog(...))
    await uow.commit()
```

### Commit
- **Explicit and single** — the Application layer calls `await uow.commit()` exactly once, at the end of the successful use case. Repositories never commit on their own (Δ from today).

### Rollback
- **Automatic on any exception** — the context manager's `__aexit__` rolls back if the block raised or `commit()` was not reached. Domain errors, integration failures, and unexpected exceptions all leave the DB unchanged.

### Ownership
- **Application owns *when* to commit** (use-case semantics); **UoW owns *how*** (session/txn lifecycle); **Repositories own *what* to read/write** but not the boundary.
- The UoW is **provider-backed** (ADR-006): `Entity Framework CoreUnitOfWork` uses `async_sessionmaker` + `session.begin()`; an asyncpg UoW uses a pool connection + `connection.transaction()`; `InMemoryUnitOfWork` stages writes and flushes on commit / discards on rollback.

## Consequences
**Positive:** true atomicity for multi-step use cases; removes per-method commits; a clean seam for the future Outbox/Saga (blueprint §29).
**Negative:** repositories must accept a UoW-supplied session; transactions' raw-asyncpg path needs a UoW wrapper (conflict C-1).
**Scope note:** the UoW gives **intra-service** atomicity. **Cross-service** money movement still needs Saga/idempotency (R-04) — deferred to the event phase; until then idempotency + circuit breaker remain in force.

## Alternatives considered
- **Keep per-repo commits:** rejected — no atomicity, the root of R-04.
- **Rely on DB-level transactions ad hoc in services:** rejected — leaks session management into Application, inconsistent across providers.



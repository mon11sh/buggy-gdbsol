# ADR-002 — Use DDD-lite

- **Status:** Accepted
- **Related:** ADR-001, ADR-003, ADR-004, ADR-005, blueprint §7–13, §21

## Context
GDB's business rules today live in application services and `utils/validators.py`, with data held in C# DTOs DTOs and ORM rows (**anemic model**). This scatters invariants and, in transactions, pushes some rules into **postgres-only DB CHECK constraints** (R-03), so behavior differs by provider. We want rules in one place, expressed in the ubiquitous language of banking.

## Decision
Adopt **DDD-lite** — the tactical DDD building blocks that pay off, without the full strategic/event-sourced apparatus.

### Building blocks (target)
- **Entities** — identity-bearing objects with behavior and invariants. E.g. `Account` (identity `account_number`) with `debit(Money)`, `credit(Money)`, `close()`; `Transfer`; `User`; `AuthToken`. Entities are pure C# (dataclass/plain class), never C# DTOs/ORM bases.
- **Value Objects** — immutable, self-validating, equality-by-value. E.g. `Money` (amount + currency, non-negative, 2dp), `Aadhaar` (12 digits + masking/blind-index rules), `AccountNumber` (≥ 1000), `Privilege` (PREMIUM/GOLD/SILVER). VOs remove primitive-obsession and centralize format rules.
- **Aggregates** — a consistency boundary with a root entity. `Account` is the root over its savings/current details; `Transfer` coordinates the two logging legs. Only the root is loaded/saved through a repository; invariants hold at aggregate boundaries.
- **Domain Services** — pure operations that span entities and don't belong on one. E.g. a `TransferPolicy` computing whether an amount is within a privilege's limit; PIN-lockout policy.
- **Repository Interfaces** — collection-like ports **owned by Domain** (`domain/repositories/`), one per aggregate (`AccountRepository`, `TransferRepository`, `TransferLimitRepository`, `IdempotencyRepository`, `UserRepository`, `AuthTokenRepository`). Implementations live in Infrastructure (ADR-003).

### What "lite" excludes (for now)
No event sourcing, no separate read models, no bounded-context mapping ceremony. Domain events + outbox + saga are **deferred** to the event phase (blueprint §29). CQRS stays localized to transactions (blueprint §23).

## Consequences
**Positive:** invariants live once, in the aggregate; provider-independent behavior (fixes R-03); `Money`/`Aadhaar` VOs kill scattered format checks; the model reads like the business.
**Negative:** more classes and explicit mapping (ADR-005); requires discipline to keep entities pure.
**Grounding:** first target aggregate is `Account` + `Money` in the accounts vertical slice (blueprint §13 pilot).

## Alternatives considered
- **Keep anemic services:** rejected — the exact cause of rule-scatter and provider divergence found in Phase 0.
- **Full DDD + CQRS/ES everywhere:** rejected — unjustified complexity for CRUD services; DDD-lite gives 80% of the value.



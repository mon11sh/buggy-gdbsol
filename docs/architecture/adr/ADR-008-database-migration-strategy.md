# ADR-008 — EF Core Migrations is the Authoritative Migration Source

- **Status:** Accepted
- **Related:** ADR-006, blueprint §11, §22, database-schema-baseline, migration-risk-register R-02, R-11

## Context
Phase 0 found that **`Base.metadata.create_all` — not EF Core Migrations — creates the live schema at startup** for accounts/users/auth; transactions builds its postgres/supabase schema from **raw `transactions_schema.sql`**. EF Core Migrations baselines exist for all four services but **are never run at startup**, and the raw `*_schema.sql` files for accounts/users/auth have **drifted** from the ORM (R-02, R-11). `create_all` does not alter existing tables, so any ORM rename silently diverges from a live DB (this already caused a stale MySQL unique-index incident historically).

## Decision
**EF Core Migrations becomes the single authoritative source of schema truth.** `create_all()` must **never** run automatically in production.

### Development vs production behavior
| Aspect | Development / test | Production |
|---|---|---|
| Schema creation | `create_all()` **allowed** for `inmemory`/`sqlite`/local convenience and fast test setup | **Forbidden automatically** — schema comes only from `EF Core Migrations upgrade head` |
| Migrations | Autogenerate + review against ORM; may reset local DBs freely | Applied as an explicit, ordered, reviewed deploy step (`EF Core Migrations upgrade head`) before app start |
| Source of truth | ORM + EF Core Migrations revisions kept in lockstep | EF Core Migrations revision history |
| Raw `*_schema.sql` | reference only | **not** a runtime source; to be retired or clearly marked (R-11) |
| transactions postgres path | may use raw SQL locally | migrated to EF Core Migrations-managed schema (removes the MIXED source-of-truth, R-03/R-11) |

### Rules
1. Every schema change ships as an **EF Core Migrations revision**, reviewed like code; no silent `create_all` drift.
2. Existing production DBs are **stamped** to the current baseline, then evolved forward only via `EF Core Migrations upgrade`.
3. A production start-up **must not** call `create_all`; a guard (config/env) enforces this (extends the existing `_assert_secure_config` fail-fast philosophy).
4. EF Core Migrations `env.py` continues to coerce async→sync drivers for offline generation; the **runtime** app uses async drivers.
5. **Backward-compatibility:** table/column/index/constraint names in `backward-compatibility-contracts.md` are frozen; migrations that rename them require an explicit compatibility decision + data-preserving migration (ADR-009).

## Consequences
**Positive:** deterministic, reviewable, reversible schema evolution; eliminates R-02 silent drift and the R-11 raw-SQL divergence; safe renames.
**Negative:** every schema change now needs a migration (intended); the transactions raw-SQL path must be converted (one-time effort).
**Pre-work (blueprint §17 Go/No-Go):** adopt EF Core Migrations-as-truth for the first vertical-slice service (accounts) before any model renames.

## Alternatives considered
- **Keep `create_all` (status quo):** rejected — silent drift, no rename safety, provider divergence.
- **Hand-written SQL migrations only:** rejected — EF Core Migrations gives autogenerate + a revision graph + Python data migrations; the baselines already exist.



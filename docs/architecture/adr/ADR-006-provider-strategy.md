# ADR-006 — Database Provider Strategy

- **Status:** Accepted
- **Related:** ADR-003, ADR-004, ADR-007, blueprint §14, §22, §33, provider-support-matrix

## Context
Phase 0 confirmed a Provider Strategy already exists: `DATABASE_PROVIDER` selects a `DatabaseProvider` via `create_provider()`. Five values are supported. accounts/users/auth route all four SQL backends through one `Entity Framework CoreProvider`; transactions routes postgres/supabase through a raw-`asyncpg` `AsyncpgProvider` and sqlite/mysql through Entity Framework Core. Only `inmemory` and (accounts) `sqlite` are actually exercised by tests — nothing is verified against a live MySQL/Postgres/Supabase server.

## Decision
Keep and standardize the **Provider Strategy**: one factory maps a provider name to a `DatabaseProvider` that yields repositories + a Unit of Work behind the domain interfaces.

### Supported providers
| Value | Backing | Driver(s) | Intended use |
|---|---|---|---|
| `inmemory` | dict store | — | tests, CI, local |
| `sqlite` | file | aiosqlite | local dev, fast integration tests |
| `supabase` | hosted Postgres | asyncpg + SSL `require` | managed cloud deployment |
| `mysql` | MySQL server | aiomysql (`pool_recycle=3600`) | on-prem / classic |
| `postgres` | PostgreSQL server | asyncpg (Entity Framework Core for accounts/users/auth; raw asyncpg for transactions) | primary production DB |

`supabase` is the Postgres path plus SSL + skip-create-database; it is a **deployment flavor of PostgreSQL**, kept as a distinct value because its connection/SSL handling differs.

### pgAdmin is NOT a provider
**pgAdmin is a graphical administration client for PostgreSQL/Supabase — not a runtime database backend.** There is no `pgadmin` value in `DATABASE_PROVIDER`, no `PgAdminProvider`, and no factory branch for it. Operators may *use* pgAdmin to inspect/administer the `postgres`/`supabase` databases, but the application always connects through the `postgres`/`supabase` providers via asyncpg. Treating pgAdmin as a provider would be a category error.

### Selection & wiring
- The **factory is the only switch** on provider name (blueprint §14). It returns a provider exposing `repositories` + `unit_of_work()` typed to the Domain interfaces (ADR-003/004).
- The **Composition Root** (ADR-007) obtains the provider and injects UoW/repos; no other layer names a provider.
- Target: reconcile transactions' asyncpg path behind the same repository/UoW interfaces so provider choice never leaks upward (blueprint conflict C-1).

### Verification requirement
"Implemented" ≠ "verified working." Before a provider is declared production-ready it must pass the service suite against a **live** instance of that engine (blueprint §32). Today only inmemory (+ accounts sqlite) is verified.

## Consequences
**Positive:** storage is a config choice; tests run on inmemory; new providers are edge-only additions (blueprint §33).
**Negative:** each provider needs real integration verification; transactions' dual path needs unification.

## Alternatives considered
- **Single hard-coded database:** rejected — loses test-in-memory and deployment flexibility.
- **ORM-only (drop asyncpg):** attractive for uniformity but deferred — transactions' asyncpg path is intentionally preserved for now; unification is a later decision.



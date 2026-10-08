# Provider Support Matrix & Resolution Flow — Phase 0 Baseline

**Scope:** the four services with a `app/database/providers/` layer — **accounts_service, transactions_service, users_service, auth_service**. The other six services (aadhar, company_crv, notification, central_payment_gateway, central_gateway, registry) have **no persistence layer** (grep for `orm_models|create_all|DATABASE_PROVIDER` returns nothing) — out of scope.

**5 provider values:** `inmemory`, `sqlite`, `mysql`, `postgres`, `supabase`. (PostgreSQL may be *administered* with pgAdmin, but pgAdmin is a client tool, **not** a provider.)

**Architectural split (confirmed in factory code):**
- **accounts / users / auth** — all four SQL backends (`sqlite, mysql, postgres, supabase`) route to a single `Entity Framework CoreProvider` (async Entity Framework Core). `inmemory` → `InMemoryProvider`.
- **transactions** — `postgres` + `supabase` route to a raw-`asyncpg` `AsyncpgProvider`; only `sqlite` + `mysql` → `Entity Framework CoreProvider`; `inmemory` → `InMemoryProvider`. It is the only service with **three** provider classes.

**Status legend**
- **IMPLEMENTED** = concrete class + repo code exists and is reachable from the factory.
- **CONFIGURED** = a settings default / URL / compose file mentions it, no distinct code path.
- **TESTED** = an automated test exercises this provider path.
- **VERIFIED WORKING** = tested against a live server of that engine.

> ⚠️ **No provider is "verified working" against a live MySQL / PostgreSQL / Supabase server in this baseline.** Tests overwhelmingly exercise `inmemory`; only accounts `test_aadhar_encryption.py` drives a live `Entity Framework CoreProvider` on **sqlite**. Everything else is IMPLEMENTED + CONFIGURED but unproven at runtime here.

---

## Matrix — accounts_service
Files: `app/database/providers/{factory,base,Entity Framework Core_provider,inmemory_provider}.py`, `app/repositories/{Entity Framework Core_repositories,inmemory_repositories,account_repo}.py`

| Provider | Impl | Factory branch (factory.py:67-82) | Connection/provider | Repo impl | Schema | Migrations | Tests | Verified live |
|---|---|---|---|---|---|---|---|---|
| inmemory | IMPLEMENTED | `if provider=="inmemory": return InMemoryProvider()` | `InMemoryProvider` (inmemory_provider.py:15) | `InMemoryAccountRepository` (inmemory_repositories.py:70) | dict store | n/a | Yes (`test_repository.py`) | n/a (in-proc) |
| sqlite | IMPLEMENTED | `if provider in (sqlite,mysql,postgres,supabase): Entity Framework CoreProvider(resolve_database_url(provider), name=provider)` | `Entity Framework CoreProvider` (Entity Framework Core_provider.py:23); URL `sqlite+aiosqlite:///./gdb_accounts.db` | `Entity Framework CoreAccountRepository` (Entity Framework Core_repositories.py:44) | `Base.metadata.create_all` (L78-79) | EF Core Migrations baseline `cfd3115280bf` (not run at startup) | Yes (`test_aadhar_encryption.py:14,28`) | **Yes (sqlite only)** |
| mysql | IMPLEMENTED | same branch | `Entity Framework CoreProvider`; `mysql+aiomysql://…` | same | `create_all`; `_ensure_database()` CREATE DB via aiomysql (L113-125) | EF Core Migrations baseline | No | No |
| postgres | IMPLEMENTED | same branch | `Entity Framework CoreProvider`; `postgresql+asyncpg://…` (Entity Framework Core driver, not raw asyncpg) | same | `create_all`; `_ensure_database()` via asyncpg (L97-111) | EF Core Migrations baseline | No | No |
| supabase | IMPLEMENTED | same branch (name="supabase") | `Entity Framework CoreProvider` (postgres path) + `connect_args={"ssl":"require"}`; skips CREATE DB (L62-64) | same | `create_all` | EF Core Migrations baseline | No | No |

Extra artifacts: `accounts_schema.sql` (raw DDL — **not** read by `Entity Framework CoreProvider`; legacy/drifted), `migrations/002_unique_aadhar.sql` (manual, not wired to EF Core Migrations). Limitation: mysql uses `pool_recycle=3600` (not `pool_pre_ping`) due to a known Entity Framework Core 2.0.23 + aiomysql issue (L69-73).

## Matrix — transactions_service
Files add `asyncpg_provider.py` + `app/database/db.py`; four repo aggregates × three impls (asyncpg/Entity Framework Core/inmemory).

| Provider | Impl | Factory branch (factory.py:66-85) | Connection/provider | Repo impl | Schema | Tests | Verified live |
|---|---|---|---|---|---|---|---|
| inmemory | IMPLEMENTED | `if provider=="inmemory": InMemoryProvider()` | `InMemoryProvider` | 4 InMemory repos (inmemory_repositories.py:78/206/441/557) | dict | Yes (CI default; `test_repositories.py`, `test_integration.py`) | n/a |
| sqlite | IMPLEMENTED | `if provider in (sqlite,mysql): Entity Framework CoreProvider(...)` | `Entity Framework CoreProvider`; `sqlite+aiosqlite:///./gdb_transactions.db` | 4 Entity Framework Core repos (Entity Framework Core_repositories.py:84/206/459/597) | `create_all` (L79-80) | Indirect (same contract as inmemory) | No |
| mysql | IMPLEMENTED | same branch | `Entity Framework CoreProvider`; `mysql+aiomysql://…`; `_ensure_database` (db-level CREATE) | 4 Entity Framework Core repos | `create_all` | No | No |
| postgres | IMPLEMENTED | `if provider in (postgres,supabase): AsyncpgProvider(name=provider)` | `AsyncpgProvider` (asyncpg_provider.py:18) → `bootstrap_asyncpg()` (db.py:83) → `asyncpg.create_pool` (db.py:120) | **raw asyncpg** repos bound to module-global `database` (transaction_repository.py:14,98) | runtime executes `transactions_schema.sql` (db.py:144-149) + inline seed | asyncpg internals not unit-tested live (tests patch to inmemory) | No |
| supabase | IMPLEMENTED | same branch (name="supabase") | `AsyncpgProvider`; `ssl_mode="require"` (db.py:91) on connect + create_pool | raw asyncpg repos | `transactions_schema.sql` | No | No |

The postgres/supabase raw-asyncpg path is intentional legacy preservation "so existing service/repo tests (which patch `app.repositories.*.database`) keep working" (asyncpg_provider.py docstring). **`DATABASE_PROVIDER` defaults to `postgres`**, so the default runtime path for transactions is the raw-asyncpg + raw-SQL path.

## Matrix — users_service & auth_service
Both factories are structurally identical to accounts (single `Entity Framework CoreProvider` branch for all four SQL providers; `InMemoryProvider` for inmemory).

| Service | inmemory | sqlite | mysql | postgres | supabase | Tests |
|---|---|---|---|---|---|---|
| users | IMPLEMENTED `InMemoryUserRepository`(46)/`InMemoryAuditRepository`(134) | IMPLEMENTED `Entity Framework CoreUserRepository`(30)/`Entity Framework CoreAuditRepository`(127); `sqlite+aiosqlite:///./gdb_users.db` | IMPLEMENTED `mysql+aiomysql` | IMPLEMENTED `postgresql+asyncpg` | IMPLEMENTED (postgres+SSL) | model/mock tests; no explicit provider-path test |
| auth | IMPLEMENTED `InMemoryAuthTokenRepository`(50)/`InMemoryAuthAuditRepository`(160) | IMPLEMENTED `Entity Framework CoreAuthTokenRepository`(65)/`Entity Framework CoreAuthAuditRepository`(244); `sqlite+aiosqlite:///./gdb_auth.db` | IMPLEMENTED `mysql+aiomysql` | IMPLEMENTED `postgresql+asyncpg` | IMPLEMENTED (postgres+SSL) | JWT/logic tests, not provider-specific |

Both ship raw `*_schema.sql` (users, auth) that are **not** executed at runtime (create_all is authoritative) plus EF Core Migrations baselines (`2943927e5a89`, `e75fd9cb5b96`) that are **not** run at startup.

---

## Cross-cutting facts (do not over-claim)
1. EF Core Migrations baselines exist for all four services but **are never invoked at startup** — runtime schema comes from `create_all` (SQL providers) or `transactions_schema.sql` (transactions asyncpg path). Migrations are decoupled from the running app.
2. Every EF Core Migrations `Entity Framework Core.url` default is a local sqlite file (`EF Core Migrations.ini:89`); `env.py` coerces async→sync drivers (`+aiosqlite`→``, `+asyncpg`→`+psycopg2`, `+aiomysql`→`+pymysql`).
3. accounts/users/auth `*_schema.sql` files are **reference DDL only** and have drifted from the ORM (see `database-schema-baseline.md`).
4. **Field-naming caveat:** transactions `resolve_database_url` reads `DB_USER/DB_PASSWORD/DB_HOST/DB_PORT/DB_NAME`; accounts/users/auth read `DATABASE_USER/DATABASE_PASSWORD/DATABASE_HOST/DATABASE_PORT/DATABASE_NAME`. Mixing `.env` conventions silently falls back to defaults. Also, **accounts `DATABASE_URL` has a non-empty default** (`postgresql://…/gdb_accounts_db`, settings.py:44) so its discrete `DATABASE_*` fields are ignored unless `DATABASE_URL` is overridden; transactions `DATABASE_URL` defaults to `""` so its `DB_*` fields are used.

---

# Provider Resolution Flow (Task 6)

Common URL coercion (`_to_async_url`, identical logic in all factories): if `DATABASE_URL` is set it wins (coerced to async driver), else the URL is built from discrete fields.
```
postgresql+psycopg2:// | postgresql+psycopg:// | postgres:// | postgresql://  → postgresql+asyncpg://
mysql+pymysql:// | mysql://                                                   → mysql+aiomysql://
sqlite://                                                                     → sqlite+aiosqlite://
```

## accounts_service (representative of users/auth)
`DATABASE_PROVIDER` env → `app/config/settings.py::Settings.DATABASE_PROVIDER` → `providers/factory.py::create_provider()` (L67) → provider → engine → session factory → repo.

| provider | create_provider branch | URL / driver | engine/pool | session factory | repo |
|---|---|---|---|---|---|
| inmemory | `InMemoryProvider()` | — | — | — | `InMemoryAccountRepository` |
| sqlite | `Entity Framework CoreProvider(resolve_database_url("sqlite"),name="sqlite")` | `sqlite+aiosqlite:///./gdb_accounts.db` / aiosqlite | `create_async_engine(url, future=True, pool_pre_ping=True)` (L69-74) | `async_sessionmaker(engine, expire_on_commit=False)` (L75) | `Entity Framework CoreAccountRepository(session_factory)` (L81) |
| mysql | `Entity Framework CoreProvider(...,name="mysql")` | `mysql+aiomysql://{DATABASE_USER[:PW]}@{host}:{port\|3306}/{name}` / aiomysql | `create_async_engine(url, future=True, pool_recycle=3600)` (L70-72) | `async_sessionmaker` | `Entity Framework CoreAccountRepository` |
| postgres | `Entity Framework CoreProvider(...,name="postgres")` | `postgresql+asyncpg://…` / asyncpg (via Entity Framework Core) | `create_async_engine(url, future=True, pool_pre_ping=True)`; `_ensure_database()` raw asyncpg CREATE DB (L97-111) | `async_sessionmaker` | `Entity Framework CoreAccountRepository` |
| supabase | `Entity Framework CoreProvider(...,name="supabase")` | `postgresql+asyncpg://…` / asyncpg | `create_async_engine(url, …, connect_args={"ssl":"require"})`; skips CREATE DB (L62-64) | `async_sessionmaker` | `Entity Framework CoreAccountRepository` |

Then `Entity Framework CoreProvider.init()` runs `conn.run_sync(Base.metadata.create_all)` (L78-79) + seeds (`seed_default_accounts`). accounts has **no asyncpg provider**; postgres vs supabase differ **only** in the SSL `connect_args` and the skipped CREATE-DATABASE probe.

## transactions_service
`DATABASE_PROVIDER` → `settings.DATABASE_PROVIDER` (default `postgres`) → `app/database/db.py::DatabaseConnection.initialize()` (L69) → `init_db()` (L43) → `get_provider()` (L34) → `factory.py::create_provider()` (L66).

| provider | branch | provider class | connection construction | driver | pool/session | repo impl |
|---|---|---|---|---|---|---|
| inmemory | `InMemoryProvider()` | InMemoryProvider | — | — | — | 4 InMemory repos |
| sqlite | `Entity Framework CoreProvider(resolve_database_url("sqlite"),name="sqlite")` | Entity Framework CoreProvider | `sqlite+aiosqlite:///./gdb_transactions.db` | aiosqlite | `create_async_engine(…, pool_pre_ping=True)` + `async_sessionmaker` | 4 Entity Framework Core repos |
| mysql | `Entity Framework CoreProvider(...,name="mysql")` | Entity Framework CoreProvider | `mysql+aiomysql://{DB_USER[:DB_PASSWORD]}@{DB_HOST}:{port\|3306}/{DB_NAME}` | aiomysql | `create_async_engine(…, pool_recycle=3600)` + `async_sessionmaker` | 4 Entity Framework Core repos |
| postgres | `AsyncpgProvider(name="postgres")` | AsyncpgProvider | `bootstrap_asyncpg()`: `asyncpg.connect(host=DB_HOST,port=DB_PORT,user=DB_USER,password=DB_PASSWORD,database="postgres",ssl=None)` to CREATE DB, then `asyncpg.create_pool(host,port,user,password,database=DB_NAME,min_size=DB_POOL_MIN_SIZE,max_size=DB_POOL_MAX_SIZE,command_timeout=DB_TIMEOUT,ssl=None)` (db.py:96-130) | **raw asyncpg** (no Entity Framework Core, no session factory) | asyncpg pool `_pool` | raw `Transaction/Log/TransferLimit/Idempotency Repository` bound to module-global `database` |
| supabase | `AsyncpgProvider(name="supabase")` | AsyncpgProvider | same `bootstrap_asyncpg()` but `ssl_mode="require"` (db.py:91) on connect + create_pool | raw asyncpg + SSL | asyncpg pool | same raw asyncpg repos |

Schema for postgres/supabase is applied by executing `transactions_schema.sql` if `transfer_limits` is missing (db.py:144-149) + inline seed INSERTs (db.py:157-209). **postgres vs supabase** diverge only by `ssl_mode` (`"require"` vs `None`); the CREATE-DATABASE step is wrapped in try/except so a permission failure on Supabase is non-fatal (db.py:115-116).



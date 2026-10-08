# Phase 0 — Enterprise Architecture Migration Baseline Report

**Project:** GDB (Global Digital Bank) — `gdb-service` (solution codebase)
**Phase:** 0 (baseline only — **no production code refactored**)
**Migration branch:** `feature/enterprise-architecture-migration-gdb-pythonfullstack`
**Companion docs (this folder):** [api-route-inventory](api-route-inventory.md) · [provider-support-matrix](provider-support-matrix.md) · [environment-variable-inventory](environment-variable-inventory.md) · [database-schema-baseline](database-schema-baseline.md) · [backward-compatibility-contracts](backward-compatibility-contracts.md) · [migration-risk-register](migration-risk-register.md)

---

## 1. Executive Summary

The GDB platform is 10 ASP.NET Core Web API microservices + a React frontend + a shared `gdb_common` library, each service internally layered (`api → services → repositories → database`). Phase 0 established a measurable, reversible baseline: an isolated migration branch, a green automated-test run across all runnable suites, a validated startup topology, and a complete inventory of routes, providers, contracts, env vars, and schema.

**Health:** strong. **680 automated tests pass, 0 fail** across the four core stateful services (accounts 213, transactions 235, users 173, auth 34) plus the runnable peripheral suites (aadhar 5, company 5, gateway 10, payment 5). Every service's ASP.NET Core Web API app constructs and boots. The architecture already implements Repository, Provider/Factory/Strategy, DI-via-Depends, CQRS (transactions), Circuit Breaker, API Gateway, and correlation-ID observability.

**Baseline gaps to carry into Phase 1 (not fixed here):** an anemic domain with **no Unit of Work and no Mapper**; **`create_all()` — not EF Core Migrations — is the live schema source** (rename = silent drift); a **live Supabase credential in the untracked root `.env`**; **gateway CORS `*` + credentials**; **`str(exc)` leakage** in aadhar/company; **provider behavior divergence** in transactions (CHECK constraints on postgres only); and **peripheral-service test suites that can't run** (missing dotnet test gdb-service-dotnet.slnx/aiofiles in their venvs).

**Recommendation: GO for Phase 1**, conditional on the two pre-work items in §17.

---

## 2. Git State
- **Repository:** `gdb-service` worktree (branch `python-fullstack-sol-v1` of `github.com/SA-BTD-Dec-2026/gdb-service`).
- **Starting branch:** `python-fullstack-sol-v1`
- **HEAD commit:** `c5d12c5 Frontend: add Close Account action (correct reference)`
- **Working tree:** nearly clean — **1 untracked file** (`ARCHITECTURE_AUDIT.md`, produced in the prior audit turn). **No staged, no modified tracked files.**
- **Existing migration branches:** none (`git branch -a | grep -iE migration|enterprise|feature` → empty).
- **Other local branches:** `main`, `gdb-fullstack`, `python-fullstack-bugs-crs-v1` (the last is checked out in the sibling bugs worktree — the `+` marker).
- **Action taken:** created `feature/enterprise-architecture-migration-gdb-pythonfullstack` from `python-fullstack-sol-v1` with `git switch -c`. Verified the untracked file carried across with **zero loss**. **No** reset/discard/force/delete/push performed. Nothing was committed or pushed.

## 3. Test Baseline
Run with each service's **own venv** (`<svc>/venv/Scripts/python.exe -m dotnet test gdb-service-dotnet.slnx -q`). Tests use mocks + `dependency_overrides`, so no live DB is required; the default provider path is inmemory.

| Service | Tests | Passed | Failed | Skipped | Errors | Time | Notes |
|---|---|---|---|---|---|---|---|
| accounts_service | 213 | 213 | 0 | 0 | 0 | 31.2s | own venv |
| transactions_service | 235 | 235 | 0 | 0 | 0 | 34.1s | own venv |
| users_service | 173 | 173 | 0 | 0 | 0 | 8.4s | own venv |
| auth_service | 34 | 34 | 0 | 0 | 0 | 9.1s | own venv (1 warning) |
| aadhar_service | 5 | 5 | 0 | 0 | 0 | 2.7s | **borrowed** accounts venv (own venv lacks dotnet test gdb-service-dotnet.slnx) |
| company_crv_service | 5 | 5 | 0 | 0 | 0 | 2.6s | **borrowed** venv |
| central_gateway_service | 10 | 10 | 0 | 0 | 0 | 2.1s | **borrowed** venv |
| central_payment_gateway_service | 5 | 5 | 0 | 0 | 0 | 2.5s | **borrowed** venv |
| notification_service | 5 | — | — | — | **blocked** | — | own venv lacks dotnet test gdb-service-dotnet.slnx; borrowed interpreter lacks `aiofiles` (service dependency) |
| registry_service | 0 | — | — | — | — | — | no test files |
| libs/gdb_common | 0 | — | — | — | — | — | no dedicated suite (exercised via accounts tests: circuit breaker, cqrs, ratelimit, discovery, internal-auth) |
| **TOTAL (runnable)** | **680** | **680** | **0** | **0** | **0** | ~93s | |

**Failures:** none. **Blocked (infrastructure, not defects):**
- **notification_service** — test file `tests/api/test_notification_service.py`, collection error `ModuleNotFoundError: No module named 'aiofiles'`. Root cause: its own venv has `aiofiles` but no `dotnet test gdb-service-dotnet.slnx`; the borrowed accounts venv has `dotnet test gdb-service-dotnet.slnx` but no `aiofiles`. Pre-existing environment gap, **not** a code failure. Resolve by installing `dotnet test gdb-service-dotnet.slnx` into the notification venv (a Phase-1 dev-setup step; out of Phase-0 scope — no libraries introduced here).
- **Peripheral venvs (aadhar/company/gateway/payment/notification)** lack `dotnet test gdb-service-dotnet.slnx` entirely; their suites were run via a borrowed sibling venv (except notification, blocked as above). Their results are valid but should be reproduced in their own venvs after dev-deps are installed.

No test was modified, skipped, or suppressed to make it pass.

## 4. Startup Baseline
Entry point for all: **`dotnet run app.main:app`** launched by `run_all.py:91` as `<venv python> -m dotnet run app.main:app --host 0.0.0.0 --port <port>` (optional `--reload`).

| Service | Port | Lifecycle hook | Health / Ready / Live |
|---|---|---|---|
| central_gateway | 8000 | none (module-level `build_gateway_app()`) | `/health` |
| accounts | 8001 | modern `lifespan` (main.py:35-81) | `/health`, `/ready`, `/live` |
| transactions | 8002 | modern `lifespan` (main.py:76-119) | `/api/v1/health`, `/ready`, `/live` |
| users | 8003 | modern `lifespan` (main.py:40-76) | `/api/v1/health`, `/ready`, `/live` |
| auth | 8004 | modern `lifespan` (main.py:29-62) | `/health`, `/ready` (503 on DB fail), `/live` |
| aadhar | 8005 | **deprecated** `@app.on_event` (main.py:47/54) | `/health`, `/ready`, `/live` |
| company_crv | 8006 | **deprecated** `@app.on_event` (main.py:47/54) | `/health`, `/ready`, `/live` |
| notification | 8007 | none | `/health`, `/ready`, `/live` |
| central_payment_gateway | 8008 | none | `/health`, `/ready`, `/live` |
| registry | 8010 | none (`create_registry_app()`) | `/health` |

- **Required dependencies:** each service has its own venv (all 10 present). Peripheral venvs are missing `dotnet test gdb-service-dotnet.slnx` (dev-only). Stateful services need a reachable DB **only** for non-inmemory providers.
- **Safe startup verification (no ports bound, no data touched):** 8 services are proven to boot by their passing **TestClient** suites (which execute the `lifespan`/startup path under inmemory). notification and registry were verified by **import-only smoke test** — both construct a `ASP.NET Core Web API` app cleanly (notification 11 routes, registry 10 routes). No configuration or production data was changed.

## 5. Route Inventory
Full inventory (≈60 routes, public + internal) in [api-route-inventory.md](api-route-inventory.md). Highlights: all user-facing routes under `/api/v1`; internal routes behind `X-Internal-API-Key` at router level; **aadhar & company import but do not apply the internal-key dependency** (their verify endpoints are unauthenticated); transactions responses are `response_model=dict`.

## 6. Provider Support Matrix
Full matrix + limitations in [provider-support-matrix.md](provider-support-matrix.md). Summary: all 5 providers are **IMPLEMENTED** for the 4 stateful services, but **only sqlite (accounts) and inmemory are TESTED**; **none is verified against a live MySQL/PostgreSQL/Supabase server** in this baseline. transactions uniquely uses raw asyncpg for postgres/supabase.

## 7. Provider Resolution Flow
Traced per provider for accounts (representative of users/auth) and transactions in [provider-support-matrix.md](provider-support-matrix.md) — env var → settings → `create_provider()` → provider class → engine/pool → session factory → repo, with real drivers (aiosqlite/aiomysql/asyncpg) and URL construction.

## 8. Current Contract Inventory
Grounded, with "Not implemented." called out explicitly:
- **Repository interfaces/impls:** accounts `AccountRepository` → Entity Framework Core/InMemory; transactions 4 aggregates × (asyncpg default + Entity Framework Core + InMemory); users `UserRepository`/`AuditRepository` → Entity Framework Core/InMemory; auth `AuthTokenRepository`/`AuthAuditRepository` → Entity Framework Core/InMemory. aadhar/company/notification/payment/gateway/registry — **Not implemented** (stateless).
- **Provider interface/impls:** per-service `DatabaseProvider(ABC)` → `Entity Framework CoreProvider` + `InMemoryProvider` (+ `AsyncpgProvider` in transactions only).
- **Service constructors:** accounts `AccountService.__init__(self)` self-resolves `get_provider().account_repository()` (DI bypass); transactions services take optional injected repos with self-constructing fallbacks + module-level singletons; users services take an explicit `repo: UserRepository`; auth `AuthService` uses `@staticmethod` methods (repos resolved in DI providers).
- **DTOs/C# DTOs:** accounts models are C# DTOs `BaseModel`; transactions/auth models are `@dataclass`; users mixes C# DTOs (requests) + dataclass (responses).
- **ORM models:** accounts (3), transactions (4), users (2), auth (2). Others — **Not implemented.**
- **Dependency providers:** `dependencies/providers.py` `get_*` functions per stateful/peripheral service (listed in the env/contract doc).
- **Integration clients:** accounts→(aadhar,company,notification); transactions→(accounts[circuit-breaker],payment,notification); auth→users.
- **Circuit breaker:** `gdb_common/resilience.py::CircuitBreaker` — **wired at exactly one call site** (transactions→accounts).
- **CQRS:** `gdb_common/cqrs.py` + `transactions/app/cqrs/` (Deposit/Withdraw/Transfer commands, 2 queries, `build_transaction_buses`). **Only in transactions.**
- **Event bus / Event sourcing / Domain events — Not implemented** (grep-confirmed).
- **Mapper classes — Not implemented** (inline `_*_to_dict` helpers only).
- **Unit of Work — Not implemented** (grep-confirmed).
- **Configuration classes:** per-service C# DTOs `Settings` + `_assert_secure_config()` guard; central_gateway uses a plain `config.py`; registry has **no** config module.

## 9. Environment Variable Inventory
13-category inventory (with a live-credential location flagged by name only) in [environment-variable-inventory.md](environment-variable-inventory.md). Key facts: production guard `_assert_secure_config()` in all 8 service Settings but **not** covering `PIN_ENCRYPTION_KEY`/users `SECRET_KEY`, and **absent** in gateway/registry; **no observability/tracing env vars**; **no external third-party credentials** (services are simulated in-repo; Supabase is the only real backend).

## 10. Database Schema Baseline
Per-table columns/keys/indexes/FKs and source-of-truth analysis in [database-schema-baseline.md](database-schema-baseline.md). **create_all() is authoritative** for accounts/users/auth; transactions is **MIXED** (raw SQL on postgres/supabase, create_all on sqlite/mysql); EF Core Migrations baselines exist but do not run at startup; raw `*_schema.sql` for accounts/users/auth is dead/drifted.

## 11. Backward-Compatibility Constraints
Frozen contracts (request/response fields, status codes, error formats, routes, headers, provider names, table/column names, ports, inter-service URLs, dual env aliases) in [backward-compatibility-contracts.md](backward-compatibility-contracts.md).

## 12. Migration Risk Register
14 ranked risks (2 Critical, 5 High, 4 Medium, 3 Low) covering all required topics in [migration-risk-register.md](migration-risk-register.md). Critical: **R-01 live Supabase credential**, **R-02 create_all as authoritative schema**.

## 13. Recommended First Vertical Slice
**Slice: the accounts_service "create + read savings account" path, migrated to Clean Architecture with UoW + Mapper + injected repository — behind unchanged routes and DTOs.**

Why this slice first:
- **Highest-value, best-understood flow** — already fully traced end-to-end (route → service → factory/strategy → repo → ORM) and covered by 213 passing tests as a regression net.
- **Exercises every target pattern once** — a real `domain/` entity (`Account`, `Money` VO), a `Mapper` (DTO↔domain↔ORM), a `UnitOfWork` around the session, and constructor-injected repository (fixes the `AccountService` DI bypass, R-08) — without touching money-movement/distributed concerns yet.
- **Contained blast radius** — accounts owns its DB; no cross-service transaction; the public contract (`POST /api/v1/accounts/savings`, `AccountResponse`, 201, Aadhaar masking) stays byte-for-byte identical, validated by the existing snapshot of routes/DTOs.
- **Proves the schema-safety approach** — forces adopting EF Core Migrations-as-source-of-truth (R-02) on one service before fleet-wide rollout.

Explicitly deferred out of slice 1: transactions/money-movement (needs Saga/Outbox — R-04), provider divergence hardening (R-03), and any gateway/security fixes (handled as Phase-1 pre-work, §17).

Success criteria: all 213 accounts tests still green; OpenAPI snapshot for accounts unchanged; `EF Core Migrations upgrade head` reproduces the current schema; encrypt/decrypt + masking tests pass.

## 14. Files Created
All under `gdb-service/docs/enterprise-migration/` (documentation only — **no application behavior changed**):
- `phase-0-baseline-report.md` (this file)
- `api-route-inventory.md`
- `provider-support-matrix.md`
- `environment-variable-inventory.md`
- `database-schema-baseline.md`
- `backward-compatibility-contracts.md`
- `migration-risk-register.md`

(Also present from the prior turn, untracked, not part of Phase 0: `ARCHITECTURE_AUDIT.md`.)

## 15. Files Modified
**None.** No production source, config, schema, test, or application file was modified in Phase 0.

## 16. Commands Executed (representative)
- `git rev-parse --abbrev-ref HEAD`, `git log -1 --oneline`, `git status --porcelain`, `git branch -a` — state inspection.
- `git switch -c feature/enterprise-architecture-migration-gdb-pythonfullstack` — branch creation (no reset/force/push).
- `<svc>/venv/Scripts/python.exe -m dotnet test gdb-service-dotnet.slnx -q --no-header` — per-service test runs (accounts/transactions/users/auth on own venv; aadhar/company/gateway/payment via borrowed accounts venv).
- `<svc>/venv/Scripts/python.exe -c "import app.main ..."` — import-only startup smoke for notification & registry.
- Read-only inspection (`grep`/read) of `run_all.py`, `main.py`, settings, providers, repositories, ORM, models — via research agents.
No destructive, schema-altering, network-pushing, or dependency-installing command was run.

## 17. Go / No-Go Decision for Phase 1

### ✅ GO — conditional on two pre-work items

**Rationale:** the codebase is healthy and well-understood — 680 tests green, all services boot, and the full contract/provider/schema surface is now documented and frozen. The migration can proceed safely and measurably.

**Mandatory pre-work before Phase-1 refactoring begins (small, low-risk, not itself a refactor):**
1. **Close R-01** — rotate the Supabase credential and confirm `.env` stays git-ignored + secret-scanned. (Security; do not carry a live secret into an active refactor branch.)
2. **Close R-02 for the first slice** — adopt EF Core Migrations as the runtime schema source (or freeze ORM names) for accounts_service, so the vertical slice can rename/introduce models without silent `create_all` drift.

**Strongly recommended alongside Phase 1 (behavior-preserving, per risk register):** gateway CORS allowlist (R-06), shared exception handler to stop `str(exc)` leakage (R-07), and installing `dotnet test gdb-service-dotnet.slnx` into every service venv so all suites — including notification — run in their own environment (R-05).

**No-Go triggers (none currently present):** an unrotated live credential reaching a shared/pushed branch; loss of the green baseline; or starting money-movement refactors before UoW/Saga exists.

Proceed to Phase 1 with the **accounts create-savings vertical slice** (§13) as the pilot. **Phase 0 ends here — no further refactoring performed.**



# Migration Risk Register — Phase 0

Ranked Critical → Low. Every risk is grounded in a real file/behavior found during the baseline. "Likelihood" = chance it bites during the enterprise migration if unmanaged.

---

## 🔴 CRITICAL

### R-01 — Live Supabase credential in the untracked root `.env`
- **Description:** `gdb-service/.env` holds a real Supabase DB password (`SUPABASE_DB_PASSWORD` and the password embedded in `SUPABASE_DATABASE_URL`). It is git-ignored (`.gitignore:11`) so not committed, but it is a live secret sitting in the working tree.
- **Affected services:** any service run with `DATABASE_PROVIDER=supabase`; docker-compose.supabase path.
- **Affected files:** `gdb-service/.env` (value never to be printed/committed/logged).
- **Impact:** credential disclosure → full DB compromise if it leaks into a commit, artifact, screen-share, or log.
- **Likelihood:** Medium (easy to `git add -f` or paste by accident during a migration).
- **Mitigation:** rotate the Supabase password now; keep `.env` git-ignored; move to a secrets manager in Phase 1; add a pre-commit secret scanner. Never echo the value.
- **Validation:** `git check-ignore .env` returns `.env`; `git log -p` contains no Supabase password; secret scanner clean.

### R-02 — `create_all()` is the authoritative schema (no migration guard)
- **Description:** accounts/users/auth build their schema at startup via `Base.metadata.create_all`; transactions uses raw `transactions_schema.sql` on postgres. EF Core Migrations baselines exist but are **never run at startup**. `create_all` does **not** alter existing tables.
- **Affected services:** accounts, users, auth, transactions.
- **Affected files:** `*/app/database/providers/Entity Framework Core_provider.py` (`create_all`), `transactions_service/app/database/db.py`, all `EF Core Migrations/versions/*`.
- **Impact:** any ORM column/table rename during migration silently diverges from existing DBs; stale unique indexes persist (already seen historically with the MySQL aadhar unique index). Refactors that assume migrations run will corrupt/mismatch schema.
- **Likelihood:** High (the migration will touch ORM/domain models).
- **Mitigation:** before Phase 1, adopt EF Core Migrations as the runtime source of truth (autogenerate from current ORM, stamp existing DBs), or freeze ORM table/column names. Never rename in-place without a migration.
- **Validation:** `EF Core Migrations upgrade head` reproduces the current create_all schema on a fresh DB for each service; round-trip diff is empty.

---

## 🟠 HIGH

### R-03 — Provider behavior divergence (transactions postgres vs sqlite/mysql)
- **Description:** transactions runs raw `transactions_schema.sql` (with CHECK constraints + `from_account<>to_account`) on postgres/supabase, but `create_all` (no CHECKs) on sqlite/mysql. Same code, different DB-level enforcement.
- **Affected services:** transactions.
- **Affected files:** `transactions_service/app/database/{db.py,transactions_schema.sql,orm_models.py}`, `providers/asyncpg_provider.py`.
- **Impact:** a transfer that DB-rejects on postgres may be accepted on sqlite/mysql (e.g. `transfer_mode=CHEQUE`, self-transfer). Tests run on inmemory and miss both.
- **Likelihood:** High.
- **Mitigation:** move invariants into the domain/service layer (not DB CHECKs) during migration; add cross-provider contract tests.
- **Validation:** identical rejection behavior across inmemory/sqlite/postgres for the invariant set.

### R-04 — Distributed money transfer has no atomic/UoW boundary
- **Description:** deposits/withdrawals/transfers coordinate across services via synchronous httpx + idempotency keys + a single circuit breaker; there is **no Unit of Work** and **no Saga**. `transfer_service` compensates manually.
- **Affected services:** transactions (→ accounts, notification, payment).
- **Affected files:** `transactions_service/app/services/{transfer_service.py,deposit_service.py,withdraw_service.py}`, `integration/account_service_client.py`.
- **Impact:** partial failure mid-transfer can leave inconsistent balances/logs; refactoring the persistence layer without preserving the idempotency/compensation flow risks double-debits.
- **Likelihood:** Medium.
- **Mitigation:** introduce UoW then Saga/Outbox in a later phase; until then, keep idempotency + compensation intact and covered by tests.
- **Validation:** fault-injection tests (fail after debit, before credit) show no balance drift; idempotent replay returns the original result.

### R-05 — Missing/blocked tests on peripheral services
- **Description:** aadhar/company/gateway/payment venvs lack `dotnet test gdb-service-dotnet.slnx`; notification requires `aiofiles` (present only in its own venv, which lacks dotnet test gdb-service-dotnet.slnx) → its 5 tests cannot run in either interpreter here. registry and libs have **no** tests.
- **Affected services:** notification (blocked), registry (none), libs/gdb_common (no dedicated suite; covered indirectly via accounts).
- **Affected files:** `*/venv/`, `notification_service/tests/`, `registry_service` (no tests).
- **Impact:** changes to notification/registry/gdb_common are unguarded; regressions invisible.
- **Likelihood:** Medium.
- **Mitigation:** install dotnet test gdb-service-dotnet.slnx into each service venv (Phase 1 setup) — a dev-dep change, out of Phase-0 scope; add a gdb_common test suite; verify `aiofiles` is in notification's runtime deps.
- **Validation:** every service's own venv runs `dotnet test gdb-service-dotnet.slnx` green.

### R-06 — Gateway CORS wildcard + credentials
- **Description:** `central_gateway_service/app/main.py:60-64` sets `allow_origins=["*"]` with `allow_credentials=True`.
- **Affected services:** central_gateway (fronts all).
- **Affected files:** `central_gateway_service/app/main.py`.
- **Impact:** browser clients can send credentialed cross-origin requests from any origin — CSRF/credential-theft exposure.
- **Likelihood:** High (already present).
- **Mitigation:** replace `*` with an explicit allowlist in Phase 1 (behavior-preserving for the known frontend origin).
- **Validation:** preflight from an unlisted origin is rejected; the real frontend origin still works.

### R-07 — Exception leakage (stack traces to clients)
- **Description:** aadhar and company_crv global handlers return `str(exc)` (`*/app/main.py:138`). accounts/users/notification/payment have **no** global handler (per-route only).
- **Affected services:** aadhar, company_crv (leak); accounts/users/notification/payment (inconsistent).
- **Affected files:** `aadhar_service/app/main.py:138`, `company_crv_service/app/main.py:138`, service `main.py` files.
- **Impact:** internal details leak; inconsistent error contracts complicate a uniform error-format migration.
- **Likelihood:** Medium.
- **Mitigation:** add one shared `gdb_common` exception-handler installer returning the standard `{error_code,message}` shape; stop leaking `str(exc)`.
- **Validation:** forced 500 returns a generic body on every service; no stack text in responses.

---

## 🟡 MEDIUM

### R-08 — Shared mutable singletons / global state
- **Description:** module-level service + client singletons (`deposit_service = DepositService()`, `_account_breaker`, `account_service_client`), the in-memory store, and the in-process rate limiter mutate shared state without locks. `AccountService.__init__` self-resolves `get_provider()` (bypasses DI).
- **Affected services:** all (pattern), especially transactions & accounts.
- **Affected files:** `transactions_service/app/services/*` (module singletons), `accounts_service/app/services/account_service.py:58-68`, `gdb_common/ratelimit.py`.
- **Impact:** hard to test/replace; incorrect across multiple workers/replicas; DI refactor may change lifecycle and surface latent ordering bugs.
- **Likelihood:** Medium.
- **Mitigation:** convert to DI-provided instances with explicit lifetimes during the DI phase; inject repos into `AccountService`.
- **Validation:** services construct with injected deps; tests pass without relying on import-time singletons.

### R-09 — Route/DTO compatibility surface is large and inconsistent
- **Description:** ~60 routes; transactions uses `response_model=dict` (weak schema) and primitive query params; auth OpenAPI paths differ; users internal prefix is `/internal/v1` vs accounts `/api/v1/internal`.
- **Affected services:** all, especially transactions, auth, users.
- **Affected files:** see `api-route-inventory.md`, `backward-compatibility-contracts.md`.
- **Impact:** a refactor that "tidies" paths/models breaks the frontend and inter-service calls.
- **Likelihood:** Medium.
- **Mitigation:** freeze the route inventory as a contract; add contract/snapshot tests before touching routers.
- **Validation:** OpenAPI snapshot per service is unchanged after refactors (except intended additions).

### R-10 — Aadhaar encryption/masking contract
- **Description:** accounts encrypts Aadhaar at rest (Fernet) + `aadhar_hash` blind index; responses mask to `********NNNN`. `PIN_ENCRYPTION_KEY` is **not** validated by the prod guard.
- **Affected services:** accounts.
- **Affected files:** `accounts_service/app/utils/encryption.py`, `models/account.py:108-114`, `database/orm_models.py`.
- **Impact:** a persistence refactor could break decrypt-on-read or the blind-index uniqueness; an insecure default PIN key could reach production.
- **Likelihood:** Medium.
- **Mitigation:** preserve `EncryptionManager` semantics; add `PIN_ENCRYPTION_KEY` to `_assert_secure_config`.
- **Validation:** round-trip encrypt/decrypt test; masked response test; prod guard rejects default PIN key.

### R-11 — Schema drift between ORM and raw `*_schema.sql`
- **Description:** accounts/users/auth raw SQL files have drifted from the ORM (users `user_audit_log` vs `user_audit_logs`, JSONB vs Text, FK differences; accounts plaintext Aadhaar).
- **Affected services:** accounts, users, auth.
- **Affected files:** `*/app/database/*_schema.sql`.
- **Impact:** anyone who mistakes the raw SQL for truth builds the wrong schema; confusion during migration.
- **Likelihood:** Low-Medium.
- **Mitigation:** delete or clearly mark the dead `*_schema.sql` as reference-only in Phase 1.
- **Validation:** only one schema source per service is live and documented.

---

## 🟢 LOW

### R-12 — Service Discovery built but unused
- **Description:** `registry_service` + `gdb_common/discovery.py` exist; runtime resolves peers via static `*_SERVICE_URL`. Gateway uses `StaticResolver`.
- **Affected services:** all (indirectly), registry, gateway.
- **Impact:** dead capability; risk only if migration assumes it's wired.
- **Likelihood:** Low.
- **Mitigation:** either wire it in a resilience phase or document it as static; don't assume dynamic discovery.
- **Validation:** documented decision; no code path depends on the registry at runtime.

### R-13 — Rate limiting off by default and in-process
- **Description:** gateway token-bucket limiter defaults to `GATEWAY_RATE_LIMIT_PER_MIN=0` (disabled); per-service limiting absent; limiter state is in-process.
- **Affected services:** central_gateway.
- **Affected files:** `central_gateway_service/app/config.py:40-46`, `gdb_common/ratelimit.py`.
- **Impact:** no protection today; won't work across replicas when enabled.
- **Likelihood:** Low.
- **Mitigation:** distributed (Redis) limiter in a scale phase; document current default.
- **Validation:** enabling the limiter throttles correctly in a single instance; scale plan noted.

### R-14 — bcrypt on the event loop
- **Description:** password/PIN hashing (bcrypt, 12 rounds) runs on the asyncio loop, not a thread pool.
- **Affected services:** auth, accounts.
- **Affected files:** `accounts_service/app/utils/encryption.py`, `auth_service` password util.
- **Impact:** latency spikes under concurrent auth load; not a correctness risk.
- **Likelihood:** Low.
- **Mitigation:** offload to `run_in_executor`/thread pool in a performance phase.
- **Validation:** load test shows no event-loop stall.

---

## Coverage of required risk topics
| Required topic | Covered by |
|---|---|
| exposed credentials | R-01 |
| provider behavior differences | R-03 |
| transaction handling | R-04, R-03 |
| distributed money transfers | R-04 |
| route compatibility | R-09 |
| DTO compatibility | R-09 |
| shared mutable singletons | R-08 |
| schema generation | R-02, R-11 |
| missing tests | R-05 |
| service discovery | R-12 |
| rate limiting | R-13 |
| CORS | R-06 |
| exception leakage | R-07 |



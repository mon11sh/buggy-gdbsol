# GDB .NET — Enterprise Remediation Plan (phased, executable)

Source of findings: `../DOTNET_ENTERPRISE_READINESS_REVIEW.md`.
Baseline: build green (0 errors), 28 tests passing, git initialized (`b6b9ea8`).
**Rule for every task:** edit → `dotnet build` green → `dotnet test` green → commit. One branch per phase.

Legend: ☐ todo · ⧗ in progress · ✅ done

---

## PHASE 1 — Critical: security + money integrity (P0)  ·  branch `fix/phase1-critical-security-integrity`

- ✅ **1.1 Secrets fail-closed.** `SecureConfigGuard` + `AllowInsecureDefaults`; secrets blanked in Accounts/Auth. (`ea6f02a`) Remove real default secret *values* from committed `*/appsettings.json` (blank them); make the startup guard reject empty/known-default `JwtSecretKey` / `InternalApiKey` / `PinEncryptionKey` in **every** environment, with a single explicit `AllowInsecureDefaults=true` dev opt-in. Update `docker-compose*.yml` to inject real values (or set the dev opt-in for the teaching stack).
  Files: `*/appsettings.json`, `AuthService/Program.cs:22-28`, `AccountsService/Program.cs:31-39`, `*/Config/Settings.cs`, `docker-compose*.yml`.
- ✅ **1.2 Gateway header hygiene.** Strip inbound `X-Internal-API-Key` at the edge. (`65be4f0`)
- ✅ **1.3 Guard `DisableAuth`.** Requires `AllowInsecureDefaults=true`. (`ea6f02a`) Refuse `DisableAuth=true` unless `AllowInsecureDefaults=true`. File: `AccountsService/Program.cs:96-101`.
- ✅ **1.4 Register written-but-dead middleware.** `GdbMiddlewareExtensions` wires SecurityHeaders + CorrelationId into all 9 services. (`5fa42ee`)
- ✅ **1.5 Atomic money movement.** `[ConcurrencyCheck]` RowVersion + `UpdateBalanceAtomicAsync` (fresh-read→apply→save→retry); debit/credit off the cached path. 2 SQLite concurrency tests. (`78983ce`)
- ✅ **1.6 Money-path tests.** 12 validator tests + 2 concurrency tests + 2 withdraw (PIN) tests + 1 transfer-saga compensation test. (`4a8b168`, `385563b`)

### ✅ PHASE 1 COMPLETE — build green, **51 tests pass (was 28)**. All P0 security + money-integrity fixes landed.

## PHASE 2 — Architecture & SOLID (P2 structural)  ·  branch `fix/phase2-architecture`
- ✅ 2.1 Remove dead scaffolding — CQRS/MediatR, hand-rolled CircuitBreaker*, Class1.cs, scratch/, empty Test1.cs. (`fad06a2`)
- ✅ 2.2 Route Transactions state through `FundTransfer` — enum status + real failure reason persisted. (`1a27bf2`)
- ✅ 2.3 Fix LSP — `FundTransfer.Status` → `TransferState` (no more `new` shadow). (`0ac0cef`)
- ✅ 2.4 Split god `AccountService` — extracted `IAccountInternalService` (debit/credit/verify-pin/lookup) + 7 tests. (`51c8362`)
- ✅ 2.5 `Money` invariants + currency (non-negative, Currency default INR, guarded Subtract). (`d072db2`)
- ◑ 2.6 Typed inter-service contract DTOs. DONE: AccountsService internal API (Validate/Debit/Credit) on the money path (`InternalAccountDto`/`InternalBalanceResult`), test-covered. REMAINING: other clients (Company/Aadhar/Notification/Payment) + VerifyPin body.
- ☐ 2.7 Centralise the DB-provider `switch` into one `Gdb.Common` extension. *(remaining)*
- ✅ 2.8 Interfaces for Transactions services (DIP). (`d…`, this branch)
- **NOTE (Phase-1 carryover):** the fail-closed secret guard (1.1) is only in Accounts + Auth; Transactions/Users/leaf services still use the old production-only check — extend `SecureConfigGuard` to them.

## PHASE 3 — Engineering quality (P1/P3)  ·  (done so far on the phase2 branch)
- ✅ 3.1 EF Core migrations. **All 4 core services** (Accounts/Transactions/Users/Auth) — InitialCreate migration targeting **Postgres** (the prod RDBMS); startup `postgres/supabase → Migrate()`, dev providers → EnsureCreated. Verified two ways: `dotnet ef database update` applies to live postgres:16 (real schema, row_version uuid, FKs), AND the running accounts service applied it at startup (`__EFMigrationsHistory` = InitialCreate). Fixed an Accounts wiring bug (was still gated on sqlite). REMAINING (optional): MySQL/SQL Server migration sets. Caveat: adopting Migrate() on an existing EnsureCreated DB needs a fresh DB/baseline.
- ☐ 3.2 Thread `CancellationToken` controller→service→repo→EF.
- ✅ 3.3 `AsNoTracking` on read queries (Account/User/TransactionLog repos). (`e…`)
- ☐ 3.4 `IOptions<Settings>` + validation.
- ☐ 3.5 RFC-7807 `ProblemDetails` + `Asp.Versioning`.
- ☐ 3.6 Consolidate validation + single PIN policy.
- ◑ 3.7 Central build governance — `global.json` (SDK pin) DONE; `Directory.Build.props`/`Directory.Packages.props`(CPM)/`.editorconfig` remaining.
- ☐ 3.8 Integration (WebApplicationFactory) + repository (Sqlite) tests + coverage gate + ArchUnitNET fitness.

## PHASE 4 — Performance & scale (P1/P2)  ·  (done so far on the phase2 branch)
- ☐ 4.1 DB-side pagination + `COUNT` for accounts/users; fix totals/`HasMore`.
- ✅ 4.2 Polly retry (2x backoff+jitter) wrapped around the breaker; timeouts added to Transactions typed clients. (`<polly>`)
- ✅ 3.6 Consistent 4-6 digit PIN policy (create/verify/domain). (`<pin>`)
- ✅ security: removed redundant Users prod-guard; stopped AuthService token-error `ex.Message` leak. (`<sec>`)
- ☐ 4.3 `IDistributedCache` (Redis) for cache/idempotency/rate-limit.
- ☐ 4.4 De-duplicate the two service-discovery implementations.

## PHASE 5 — Enterprise hardening (P1/P3)  ·  branch `fix/phase5-hardening`
- ✅ 5.4 (partial) Non-root Dockerfiles for all 10 services (`chown` + `USER app`). Live docker-build verify pending (daemon down).
- ☐ 5.1 Real `AddHealthChecks` (DB + downstream) → `/health/ready`.
- ☐ 5.2 Structured JSON logging + correlation; OpenTelemetry exporter.
- ☐ 5.3 Wire `MetricsMiddleware` + `/metrics` in every service.
- ☐ 5.4 Non-root Dockerfiles + `HEALTHCHECK`; `npm ci` for frontend.
- ☐ 5.5 HTTPS/HSTS (or document TLS-termination contract).
- ☐ 5.6 Enforce fleet-wide token revocation + refresh tokens.
- ☐ 5.7 CI: coverage gate, provider-matrix tests, deploy stage, NuGet cache; reconcile docs with runtime.

---

### Progress log
- `b6b9ea8` baseline snapshot — build green, 28 tests pass.
- `ea6f02a` P0-1 + P0-3: SecureConfigGuard, blanked secrets, DisableAuth guard, 6 new tests — build green, 34 tests pass.
- `65be4f0` P0-2: gateway strips inbound X-Internal-API-Key — build green.
- `5fa42ee` P1-5: SecurityHeaders + CorrelationId wired into all 9 services — build green.
- `78983ce` P0-3: atomic concurrency-safe balance updates (RowVersion + UpdateBalanceAtomicAsync) — 2 tests.
- `4a8b168` P0-4: 12 money-guard validator tests.
- **Phase 1 status: build green, 48 tests pass (was 28). Items 1.1–1.5 complete; 1.6 partial (validators+concurrency done, withdraw/transfer-saga orchestration tests remaining).**
- **Next:** finish 1.6 orchestration tests, then Phase 2 (architecture).

# GDB Business Capability & Migration-Grouping Report

**Scope:** `gdb-service` (10 services + frontend + `libs/gdb_common`). Analysis only — no code changed. Grounded in the actual codebase and the Phase 0 baseline docs. Purpose: choose the Phase 3 vertical slice(s).

---

## 1. Executive Summary
GDB is a 10-service ASP.NET Core Web API banking platform (DB-per-service, sync HTTP, JWT/RBAC, internal API-key). Business capability concentrates in **two aggregates**: **Accounts** (account lifecycle + balance + PIN + KYC) and **Money Movement** (deposit/withdraw/transfer + limits + logs + idempotency). Auth and Users are supporting identity services; aadhar/company/notification/payment are stateless simulated collaborators; gateway and registry are infrastructure. The cleanest, lowest-risk first vertical slice is **Accounts "create + read savings account"** — it is self-contained (accounts owns its DB, no cross-service transaction), fully covered by 213 tests + the OpenAPI/architecture gates, and exercises every target pattern once. Money Movement is the natural *second* slice but must wait: it spans a distributed debit→credit with no true transaction boundary and is the highest-risk area (needs UoW + Saga groundwork).

---

## 2. Microservice Inventory

| # | Service | Port | Responsibility | Database | Providers | Patterns | Maturity |
|---|---|---|---|---|---|---|---|
| 1 | **accounts_service** | 8001 | Savings/Current account lifecycle, balance, PIN, Aadhaar encryption | `gdb_accounts_db` | inmemory, sqlite, mysql, postgres, supabase | Repository, Factory (`AccountFactory`), Strategy (Savings/Current impl), Provider strategy, DI-via-Depends | **High** (213 tests; richest domain) |
| 2 | **transactions_service** | 8002 | Deposits, withdrawals, transfers, limits, logs, idempotency | `gdb_transactions_db` | inmemory, sqlite, mysql, postgres, supabase (postgres/supabase via raw asyncpg) | Repository (3 impls), CQRS, Circuit Breaker, Idempotency, Provider strategy | **High** (235 tests; most complex) |
| 3 | **users_service** | 8003 | User CRUD, roles, credential verification, audit | `gdb_users_db` | inmemory, sqlite, mysql, postgres, supabase | Repository, DI, audit logging | **High** (173 tests) |
| 4 | **auth_service** | 8004 | JWT issue/verify/revoke, login throttling | `gdb_auth_db` | inmemory, sqlite, mysql, postgres, supabase | Repository, JWT (RS256/HS256), token revocation, audit | **Medium-High** (34 tests) |
| 5 | **aadhar_service** | 8005 | Simulated UIDAI Aadhaar verification | none (in-memory list) | n/a | Stateless service | **Low** (5 tests; mock) |
| 6 | **company_crv_service** | 8006 | Simulated company registration verification | none (in-memory list) | n/a | Stateless service | **Low** (5 tests; mock) |
| 7 | **notification_service** | 8007 | Send/list/read/clear notifications | JSON file (`data/notifications.json`) | n/a (file store) | File-backed store | **Low** (6 tests) |
| 8 | **central_payment_gateway_service** | 8008 | Simulated payment process/validate | none | n/a | Stateless service | **Low** (5 tests; mock) |
| 9 | **central_gateway_service** | 8000 | API Gateway: reverse proxy, CORS, rate-limit, correlation-id | none | n/a | Reverse proxy, StaticResolver, token-bucket limiter | **Medium** (10 tests) |
| 10 | **registry_service** | 8010 | Service discovery registry (register/heartbeat/resolve) | in-memory | n/a | Registry (**built but unused at runtime**) | **Low** (0 tests) |

**Public vs Internal APIs:** accounts, users have both public (`/api/v1/...`) and internal (`/api/v1/internal`, `/internal/v1`) routers; transactions/auth are public; aadhar/company expose `/api/v1/...` (verify endpoints are **unauthenticated** — internal-key imported but not applied); notification/payment are internal-only. Full route list: [api-route-inventory.md](api-route-inventory.md).

**External integrations:** none real — aadhar/company/payment are simulated in-repo; Supabase is the only real external backend. No Stripe/Twilio/SMTP.

---

## 3. Business Feature Inventory

### accounts_service
- **Create Savings Account** — age ≥ 18, Aadhaar verified (aadhar service) + encrypted at rest + blind-index unique, blacklist check, PIN hashed, initial_balance ≥ 2000, privilege default SILVER.
- **Create Current Account** — company registration verified (company CRV), registration_no unique, no age restriction, PIN hashed.
- **Get Account** (by number) · **Get All Accounts** (filter by type, pagination) · **Accounts Summary/stats** (totals + privilege histogram + sample).
- **Get Balance** · **Update Account** (name/privilege/phone/company/website).
- **Activate / Inactivate (Suspend) / Close Account** (ADMIN).
- **Verify PIN** (with lockout → 423 after max attempts).
- **Internal:** get account details, get privilege, check active, **debit**, **credit**, verify-pin (all behind `X-Internal-API-Key`).

### transactions_service
- **Deposit** (idempotent; min 1.00; credits via accounts internal API; notifies).
- **Withdraw** (PIN verify; balance check; idempotent; debits).
- **Transfer** (PIN; transfer mode; daily/per-txn limit check; idempotent; debit source + credit target + two log legs; manual compensation on partial failure).
- **Transaction History** — all logs (paged/filtered by type/date/sort), by account, summary by account; frontend variants (`/transactions`, `/transactions/account/{n}`).
- **Transfer Limits** — get limit, get remaining, list all rules, check-can-transfer, **update rule** (ADMIN/TELLER).
- **Transaction-log analytics** — summary stats, by date range, by reference id, file logs, delete old logs (retention).
- **Idempotency** — `Idempotency-Key` reserve/replay/complete across deposit/withdraw/transfer.

### users_service
- **Add User** (ADMIN; login_id unique, password ≥ 8, role MANAGER/TELLER/ADMIN).
- **Edit User** (ADMIN) · **View User** (self or ADMIN) · **List Users** (ADMIN/TELLER).
- **Activate / Inactivate User** (ADMIN).
- **Internal:** verify credentials, get status, get role, validate role, **bulk validate**, audit-log writes.

### auth_service
- **Login** (throttle/lockout; verifies credentials via users internal API; issues JWT RS256-or-HS256 with `sub/login_id/role/jti/exp`).
- **Verify Token** · **Logout** (revoke `jti`) · **Register** (intentionally disabled → 403).
- Token-revocation store + auth audit logging.

### aadhar_service
- **Verify Aadhaar** (GET + POST; validates against in-memory dataset; returns `is_valid`/`status`) · **List valid Aadhaar numbers**.

### company_crv_service
- **Verify Company** (GET + POST) · **List valid registration numbers**.

### notification_service
- **Send Notification** (internal) · **Get Notifications** (by identifier) · **Mark all as Read** · **Clear Notifications** (JSON-file backed).

### central_payment_gateway_service
- **Process Payment** (internal) · **Validate Payment/Transfer** (internal).

### central_gateway_service
- **Reverse-proxy routing** (first path segment → backend) · **CORS** · **Rate limiting** (off by default) · **Correlation-id propagation** · gateway health.

### registry_service
- **Register / Heartbeat / Deregister / Resolve / List services / Health** (round-robin, TTL) — **built but not used at runtime** (services resolve peers via static config).

---

## 4. Feature Dependency Matrix (key features)

| Feature | Owner | Calls | Repos | DTOs | Providers | External/Internal APIs | Tables modified | Business rules |
|---|---|---|---|---|---|---|---|---|
| Create Savings | accounts | aadhar (verify), notification (send) | AccountRepository | SavingsAccountCreate → AccountResponse | 5 | aadhar `/verify`, notify `/send` | accounts, savings_account_details | age≥18, aadhar valid+unique(blind idx)+encrypted, not blacklisted, PIN hashed, balance≥2000 |
| Create Current | accounts | company (verify), notification | AccountRepository | CurrentAccountCreate → AccountResponse | 5 | company `/verify`, notify `/send` | accounts, current_account_details | registration valid+unique, PIN hashed |
| Get/List/Balance/Summary | accounts | — | AccountRepository | AccountResponse/BalanceResponse | 5 | — | (read) | RBAC ADMIN/TELLER/MANAGER |
| Activate/Inactivate/Close | accounts | — | AccountRepository | dict | 5 | — | accounts (is_active, closed_date) | ADMIN; state transitions |
| Verify PIN | accounts | — | AccountRepository | PinVerifyRequest | 5 | — | (read) | lockout after N attempts → 423 |
| Debit/Credit (internal) | accounts | — | AccountRepository | primitives | 5 | internal key | accounts.balance | amount>0; atomic single-statement update |
| Deposit | transactions | accounts (credit), notification | TransactionLog + Idempotency | DepositRequest/primitives → dict | 5 | accounts internal (breaker), notify | transaction_logging, idempotency_keys | min 1.00; idempotent |
| Withdraw | transactions | accounts (verify-pin, debit), notification | TransactionLog + Idempotency | WithdrawRequest → dict | 5 | accounts internal (breaker), notify | transaction_logging, idempotency_keys | PIN valid; sufficient balance; idempotent |
| Transfer | transactions | accounts (debit+credit), payment(validate), notification | Transaction, TransactionLog, TransferLimit, Idempotency | FundTransferCreate/TransferRequest → dict | 5 | accounts internal (breaker), payment, notify | fund_transfers, transaction_logging×2, idempotency_keys | PIN; within daily+per-txn limit for privilege; mode∈{NEFT,RTGS,IMPS,UPI} (DTO also allows CHEQUE — DB rejects on pg); idempotent; compensation on partial failure |
| Transfer Limits (get/check/update) | transactions | — | TransferLimitRepository | dict | 5 | — | transfer_limits | rules PREMIUM/GOLD/SILVER(/BASIC fallback) daily+per-txn+count |
| Transaction History/Analytics | transactions | — | TransactionLogRepository | dict | 5 | — | (read) | RBAC; owner or ADMIN/TELLER/MANAGER |
| Login | auth | users (verify credentials) | AuthTokenRepository, AuthAuditRepository | LoginRequest → TokenResponse | 5 | users internal | auth_tokens, auth_audit_logs | throttle/lockout; RS256/HS256; jti |
| Verify/Logout | auth | — | AuthTokenRepository | dict | 5 | — | auth_tokens (is_revoked) | jti revocation |
| Add/Edit/View/List/Activate User | users | — | UserRepository, AuditRepository | Add/Edit/View/List User req/resp | 5 | — | users, user_audit_logs | login_id unique; pwd≥8; RBAC ADMIN(/TELLER) |
| Verify Aadhaar | aadhar | — | none | AadharVerification req/resp | n/a | — | (none) | membership in dataset |
| Verify Company | company | — | none | CompanyVerification req/resp | n/a | — | (none) | membership in dataset |
| Send/Get Notification | notification | — | none (JSON file) | NotificationRequest | n/a | — | notifications.json | internal key |
| Process/Validate Payment | payment | — | none | Payment/Validation req/resp | n/a | — | (none) | internal key; simulated |

---

## 5. Service Communication Diagram

```
                         ┌─────────────────────────────┐
                         │   Client / React (:3000)     │
                         └───────────────┬──────────────┘
                                         │ HTTPS, Authorization: Bearer <JWT>
                                         ▼
                         ┌─────────────────────────────┐
                         │ central_gateway_service :8000│  CORS, rate-limit(off), X-Correlation-ID
                         │ StaticResolver (path→backend)│  (no auth; forwards headers)
                         └──┬───────┬───────┬───────┬───┘
        ┌───────────────────┘       │       │       └───────────────────┐
        ▼                           ▼       ▼                           ▼
  ┌───────────┐   login       ┌──────────┐  ┌────────────┐        ┌──────────────┐
  │ auth :8004│ ───internal──▶ │users:8003│  │accounts:8001│       │transactions  │
  │           │  verify creds  │          │  │            │        │   :8002      │
  └───────────┘                └──────────┘  └────┬───────┘        └──────┬───────┘
   gdb_auth_db                  gdb_users_db       │ internal (X-Internal-API-Key)   │
                                                   │  verify/debit/credit            │
                                     ┌─────────────┴────────┐          ┌─────────────┼───────────────┐
                                     ▼                      ▼          ▼             ▼               ▼
                               ┌──────────┐          ┌───────────┐ accounts    ┌──────────┐   ┌──────────┐
                               │aadhar8005│          │company8006│ (credit/    │payment   │   │notif 8007│
                               │(savings) │          │(current)  │  debit,     │  :8008   │   │          │
                               └──────────┘          └───────────┘  breaker)   └──────────┘   └──────────┘

registry_service :8010  — register/heartbeat/resolve — BUILT, NOT wired at runtime (static URLs used).

Legend / cross-cutting:
  Protocol:            synchronous HTTP (httpx), JSON.
  Public auth:         Authorization: Bearer <JWT> (RS256/HS256), RBAC ADMIN/TELLER/MANAGER.
  Internal auth:       X-Internal-API-Key (constant-time compare) on internal routers.
  Tracing:             X-Correlation-ID minted/propagated by gdb_common on every hop.
  Idempotency:         Idempotency-Key header on deposit/withdraw/transfer.
  Circuit breaker:     ONLY transactions → accounts (_account_breaker: threshold=5, reset=30s).
  Retry:               NONE anywhere.
  Timeouts:            per-client 2–10s (accounts client = ACCOUNT_SERVICE_TIMEOUT=10; auth→users 2–5s).
```

**Incoming/Outgoing summary**

| Service | Outgoing calls | Incoming calls |
|---|---|---|
| gateway | all backends (proxy) | client |
| auth | users (verify credentials) | gateway/client |
| users | — | gateway/client, auth (internal), transactions (indirect via roles) |
| accounts | aadhar, company, notification | gateway/client, transactions (internal debit/credit/verify) |
| transactions | accounts (breaker), payment, notification | gateway/client |
| aadhar/company/payment/notification | — | accounts, transactions (internal) |
| registry | — | (none at runtime) |

---

## 6. End-to-End Business Flows

**Savings Account Creation**
```
Client → Gateway → accounts POST /api/v1/accounts/savings (RBAC ADMIN/TELLER)
  → validate SavingsAccountCreate → blacklist check → age≥18
  → AadharClient.verify_aadhar → aadhar :8005 /api/v1/verify
  → EncryptionManager: hash PIN, encrypt Aadhaar + blind index
  → AccountRepository.create_savings_account → accounts + savings_account_details (commit)
  → NotificationClient.send (best-effort) → notification :8007
  → 201 AccountResponse (Aadhaar masked ********NNNN)
```

**Current Account Creation**
```
Client → Gateway → accounts POST /api/v1/accounts/current
  → validate CurrentAccountCreate → CompanyClient.verify_company → company :8006
  → repo.create_current_account → accounts + current_account_details (commit) → notify → 201
```

**Money Transfer**
```
Client → Gateway → transactions POST /api/v1/transfers (RBAC MANAGER/TELLER, Idempotency-Key)
  → Idempotency.reserve → AccountServiceClient.verify-pin + get privilege (breaker)
  → TransferLimit check (daily + per-txn for privilege) → amount/mode validation
  → accounts debit(from) → accounts credit(to)   [NO distributed transaction; manual compensation]
  → log two legs (transaction_logging) + fund_transfers → notify → Idempotency.complete → 201
```

**Deposit / Withdraw**
```
Deposit:  Gateway → transactions /deposits → Idempotency.reserve → accounts.credit (breaker) → log → notify → 201
Withdraw: Gateway → transactions /withdrawals → Idempotency.reserve → accounts.verify-pin + balance → accounts.debit → log → notify → 201
```

**Authentication / Login**
```
Client → Gateway → auth POST /api/v1/auth/login → UserServiceClient.verify credentials → users /internal/v1/users/verify
  → throttle check → issue JWT (jti persisted in auth_tokens) → 200 TokenResponse
Verify: any service validates Bearer via gdb_common JWTValidator (RS256 then HS256).
Logout: auth marks jti revoked.
```

**User Registration / Management**
```
Client(ADMIN) → Gateway → users POST /api/v1/users → add_user (login_id unique, pwd≥8) → users table + audit log → 201
(Public self-registration is DISABLED in auth → 403.)
```

**PIN Validation**
```
Public: accounts POST /accounts/{n}/verify-pin (get_current_user) → lockout tracking → 200 / 401 / 423.
Internal: accounts /internal/.../verify-pin (used by transactions withdraw/transfer).
```

**Notifications** — fire-and-forget from accounts/transactions to notification `/api/v1/notify/send` (internal key); non-fatal on failure.

---

## 7. Data Ownership Matrix

| Database | Owner | Tables (entities) | Write owner | Read-only consumers | Shared data |
|---|---|---|---|---|---|
| `gdb_accounts_db` | accounts | accounts, savings_account_details, current_account_details | accounts only (incl. internal debit/credit) | transactions (via accounts **API**, never direct DB) | none direct |
| `gdb_transactions_db` | transactions | fund_transfers, transaction_logging, transfer_limits, idempotency_keys | transactions only | — | account_number is a **reference** to accounts (no FK across DBs) |
| `gdb_users_db` | users | users, user_audit_logs | users only | auth (via users **API**) | login_id/role referenced by auth JWT claims |
| `gdb_auth_db` | auth | auth_tokens, auth_audit_logs | auth only | — | user_id/login_id referenced (no FK) |
| notification file | notification | notifications.json | notification only | — | recipient identifier |

**Key rule (already honored):** no service touches another service's database; cross-service data is obtained via API + internal key. `account_number`, `login_id`, `user_id` are cross-service **references**, not foreign keys. FKs (CASCADE) exist only within accounts (account details → accounts).

---

## 8. Provider Usage Matrix

| Service | inmemory | sqlite | mysql | postgres | supabase | Notes |
|---|---|---|---|---|---|---|
| accounts | ✅ | ✅ | ✅ | ✅ | ✅ | all SQL via Entity Framework CoreProvider |
| transactions | ✅ | ✅ | ✅ | ✅ | ✅ | **postgres/supabase via raw asyncpg**; sqlite/mysql via Entity Framework Core |
| users | ✅ | ✅ | ✅ | ✅ | ✅ | Entity Framework CoreProvider |
| auth | ✅ | ✅ | ✅ | ✅ | ✅ | Entity Framework CoreProvider |
| aadhar/company/notification/payment/gateway/registry | — | — | — | — | — | no persistence layer |

**Verified in tests:** only `inmemory` (all) + `sqlite` (accounts encryption test). No live MySQL/Postgres/Supabase verification exists. Default `DATABASE_PROVIDER=postgres`. pgAdmin is a GUI, **not** a provider. Detail: [provider-support-matrix.md](provider-support-matrix.md).

---

## 9. Integration Client Inventory

| Client | Location | Target | Methods / endpoints | Timeout | Circuit breaker | Retry | Auth |
|---|---|---|---|---|---|---|---|
| `AadharClient` | accounts/integration | aadhar :8005 | verify_aadhar → `/api/v1/verify` | per-client (~5s) | ❌ | ❌ | X-Internal-API-Key + X-Correlation-ID |
| `CompanyClient` | accounts/integration | company :8006 | verify_company → `/api/v1/company/verify` | per-client (~5s) | ❌ | ❌ | internal key |
| `NotificationClient` | accounts/integration | notification :8007 | send → `/api/v1/notify/send` | per-client (~2s) | ❌ | ❌ | internal key |
| `AccountServiceClient` | transactions/integration | accounts :8001 | validate/verify-pin, debit, credit, get privilege/active → `/api/v1/internal/...` | ACCOUNT_SERVICE_TIMEOUT=10 | ✅ `_account_breaker` (threshold=5, reset=30s) | ❌ | internal key |
| `PaymentGatewayClient` | transactions/integration | payment :8008 | process, validate → `/api/v1/payment/...` | per-client (~2–5s) | ❌ | ❌ | internal key |
| `NotificationClient` | transactions/integration | notification :8007 | send → `/api/v1/notify/send` | per-client (~2s) | ❌ | ❌ | internal key |
| `UserServiceClient` | auth/integration | users :8003 | verify credentials, status, role → `/internal/v1/users/...` | per-client (~2–5s) | ❌ | ❌ | internal key |

**Consistent gaps:** retry is not implemented anywhere; circuit breaker guards only the single most-critical path (transactions→accounts). All clients forward `X-Correlation-ID`.

---

## 10. Recommended Migration Groups

**Group A — Account Lifecycle (Accounts aggregate)**
- Create Savings, Create Current, Get/List/Balance/Summary, Update, Activate, Inactivate, Close, Verify PIN, internal Debit/Credit.
- KYC collaborators used *as ports*: Aadhaar verify (savings), Company verify (current).
- *Reason:* one aggregate (`Account`), one repository, one DB/transaction boundary, one domain. Aadhaar/Company are outbound *ports* (not owned), so their interfaces migrate with this group but their services stay as-is.

**Group B — Money Movement (Transactions aggregate)**
- Deposit, Withdraw, Transfer, Transfer Limits, Transaction Log/History/Analytics, Idempotency.
- *Reason:* shared transaction boundary + idempotency; depends on accounts debit/credit (a port). Requires UoW **and** a Saga/compensation story for the cross-service debit→credit — do *after* Group A proves the pattern.

**Group C — Identity (Auth + Users)**
- Login/Verify/Logout/Revocation (auth) + User CRUD/roles/credential-verify (users).
- *Reason:* tightly coupled (auth calls users to verify credentials); two aggregates but one identity concern. Migrate as a pair after Accounts.

**Group D — Supporting simulators (do not migrate as aggregates)**
- Aadhaar, Company, Payment, Notification — stateless; expose them behind **ports/adapters** only. No domain/UoW needed.

**Group E — Infrastructure (leave as-is this migration)**
- Gateway, Registry — no business aggregate.

---

## 11. Recommended Phase 3 Vertical Slice

**Recommendation: Group A, narrowed to the Accounts "Create Savings + Read Account/Balance" slice.**

**Why this first (grounded):**
- **Self-contained blast radius:** accounts owns `gdb_accounts_db`; the write path touches only `accounts` + `savings_account_details` with an intra-service transaction — **no distributed transaction**, unlike Money Movement.
- **Best regression net:** 213 passing accounts tests + the OpenAPI gate (`accounts_service.json`) + the Phase-2 architecture tests already guard this surface.
- **Exercises every target pattern once:** a real `Account` aggregate + `Money`/`Aadhaar` value objects, an `AccountMapper` (DTO↔domain↔ORM), a `Entity Framework CoreUnitOfWork` + `Entity Framework CoreAccountRepository` behind the Phase-2 contracts, and an accounts `CompositionRoot` using `ProviderRegistry` — all behind the unchanged, gate-protected API.
- **KYC as a port:** Aadhaar verification becomes an outbound port with an adapter, proving the ports-and-adapters seam without migrating aadhar_service.
- **Forces EF Core Migrations-as-truth** (ADR-008) on one service before fleet rollout.

**What belongs in the slice:** Create Savings, Get Account, Get Balance (the read side), the `Account` aggregate + `Money`/`Aadhaar` VOs, mapper, UoW, repository, composition root, Aadhaar port. Optionally Create Current in the same slice (same aggregate/repo) — acceptable but adds the Company port; keep it a fast-follow if scope must stay tight.

**What must NOT be migrated yet:**
- **Money Movement (Group B)** — distributed debit→credit needs UoW + Saga; highest risk (R-04).
- **Auth/Users (Group C)** — after Accounts proves the template.
- **Internal Debit/Credit** as *domain* — keep the existing API contract; migrate the read/create path first, leave balance-mutation-by-transactions untouched until Group B.
- **Aadhaar/Company/Payment/Notification/Gateway/Registry** — never as aggregates; only as ports/adapters.

---

## 12. Risks
- **R-Distributed-Money (High):** transfer/deposit/withdraw span services with no true transaction boundary; migrating them prematurely risks balance drift. → keep for Group B with UoW+Saga.
- **R-Provider-Divergence (High):** transactions uses raw asyncpg (postgres/supabase) vs Entity Framework Core (sqlite/mysql); CHECK constraints exist only on postgres (e.g. `transfer_mode` CHEQUE). → don't rely on DB-level rules; move to domain.
- **R-Unverified-Providers (Medium):** only inmemory/sqlite are tested; MySQL/Postgres/Supabase unverified. → verify against live engines during the slice.
- **R-Contract-Drift (Medium):** ~60 frozen routes/DTOs; internal debit/credit + Aadhaar masking are contracts. → OpenAPI gate must stay green.
- **R-Anemic-Coupling (Medium):** `AccountService` self-resolves `get_provider()`; module-level singletons in transactions. → fix via composition root during the slice (accounts) / later (transactions).
- **R-Operational (Carry-over):** rotate the live Supabase credential (R-01); gates not yet in CI.

## 13. Recommendations
1. **Adopt Group A (Create-Savings + Read) as the Phase 3 slice**; keep Create Current as an in-aggregate fast-follow.
2. **Model `Account` aggregate + `Money`/`Aadhaar` VOs**, an explicit `AccountMapper`, a `Entity Framework CoreUnitOfWork`/`Repository`, and an accounts `CompositionRoot` (`ProviderRegistry`) — behind the unchanged API.
3. **Treat Aadhaar/Company/Notification/Payment as ports** with thin adapters; do not migrate their services.
4. **Make EF Core Migrations authoritative for accounts** first; verify the slice against sqlite + a live Postgres.
5. **Defer Money Movement (Group B)** until the Accounts slice validates UoW/mapper/DI; design Saga/compensation before touching transfers.
6. **Keep the OpenAPI + architecture gates green** on every slice; wire them into CI.
7. **Migrate Identity (Group C) third**, as an auth+users pair.



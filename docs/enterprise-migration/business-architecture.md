# GDB Business Architecture (Reverse-Engineered Reference)

**Scope:** `gdb-service` — reverse-engineered from the actual source. Analysis/documentation only; no code changed. Every claim is grounded; missing capabilities are marked **Not Implemented**.

> **Tactical-DDD reality check (read first).** No microservice currently implements rich **domain models, aggregates, value objects, domain services, ports, adapters, use cases, mappers, unit of work, or domain events** — verified by scan. Services are **anemic**: `API → Service → Repository/IntegrationClient → ORM/DB`. The Phase-2 tactical contracts (`gdb_common.domain/application/composition`) exist but are **unused by services**. What DOES exist per service: DTOs, ORM models, **repository interfaces + implementations**, integration HTTP clients, and service classes holding the business logic. Wherever this document lists Aggregates/VOs/Ports/Adapters/UseCases/Mappers/UoW/Events, the current status is **Not Implemented** unless stated otherwise.

---

## 1. Executive Summary
GDB is a 10-service ASP.NET Core Web API banking platform (DB-per-service, synchronous HTTP, JWT/RBAC, internal API-key, correlation-id). Business value concentrates in **Accounts** (account lifecycle, balance, PIN, KYC) and **Money Movement** (deposit/withdraw/transfer, limits, logs, idempotency); **Identity** (auth+users) supports them; aadhar/company/payment/notification are stateless simulated collaborators; gateway/registry are infrastructure. The architecture standard is frozen but **not yet adopted** by any service. The lowest-risk first feature to migrate is **Savings Account Creation** (self-contained aggregate, no distributed transaction, 213 tests). Money Movement is highest-risk (distributed debit→credit, no transaction boundary).

## 2. Microservice Inventory

### accounts_service (:8001) — DB `gdb_accounts_db`
- **Purpose/Responsibilities:** savings & current account lifecycle, balance, PIN, Aadhaar encryption, KYC gating.
- **Business capabilities:** account creation (savings/current), read/list/summary, balance, update, activate/inactivate/close, verify-PIN, internal debit/credit.
- **REST APIs:** `POST /api/v1/accounts/savings|current`, `GET /accounts`, `/accounts/summary`, `/accounts/{n}`, `/accounts/{n}/balance`, `PUT /accounts/{n}`, `POST /accounts/{n}/activate|inactivate|close|verify-pin`.
- **Internal APIs:** `GET /api/v1/internal/accounts/{n}` (+ `/privilege`, `/active`), `POST /internal/accounts/{n}/debit|credit|verify-pin`.
- **DB tables:** `accounts`, `savings_account_details`, `current_account_details`.
- **Providers:** inmemory, sqlite, mysql, postgres, supabase.
- **Dependencies / external calls:** aadhar (verify), company_crv (verify), notification (send).
- **Events:** Not Implemented (future: `AccountOpened`).

### transactions_service (:8002) — DB `gdb_transactions_db`
- **Purpose:** deposits, withdrawals, transfers, transfer limits, transaction logs, idempotency.
- **REST APIs:** `POST /api/v1/deposits|withdrawals|transfers`; `GET /transaction-logs` (+ `/{n}`, `/summary/{n}`); transfer-limits get/remaining/rules-all/check + `PUT /transfer-limits/rules/{privilege}`; frontend `POST /api/v1/transactions/deposit|withdraw|transfer`, `GET /api/v1/transactions`, `/transactions/account/{n}`.
- **DB tables:** `fund_transfers`, `transaction_logging`, `transfer_limits`, `idempotency_keys`.
- **Providers:** inmemory, sqlite, mysql, postgres, supabase (postgres/supabase via raw asyncpg).
- **Dependencies / external calls:** accounts (verify-pin/debit/credit — circuit-breaker), payment (validate/process), notification (send).
- **Patterns present:** Repository (3 impls/aggregate), CQRS (`app/cqrs`), Circuit Breaker, Idempotency.
- **Events:** Not Implemented (future: `FundsTransferred`, `Deposited`, `Withdrawn`).

### users_service (:8003) — DB `gdb_users_db`
- **Purpose:** user CRUD, roles, credential verification, audit.
- **REST APIs:** `POST /api/v1/users`, `GET /users` (list), `GET /users/{login_id}`, `PUT /users/{login_id}`, `PATCH /users/{login_id}/activate|inactivate`.
- **Internal APIs:** `/internal/v1/users/verify`, `/status`, `/role`, `/validate-role`, `/bulk-validate`, `/health`.
- **DB tables:** `users`, `user_audit_logs`. **Providers:** all 5. **External calls:** none. **Events:** Not Implemented.

### auth_service (:8004) — DB `gdb_auth_db`
- **Purpose:** JWT issue/verify/revoke, login throttling.
- **REST APIs:** `POST /api/v1/auth/login`, `GET /auth/verify`, `POST /auth/logout`, `POST /auth/register` (**disabled → 403**), `GET /auth/health`.
- **DB tables:** `auth_tokens`, `auth_audit_logs`. **Providers:** all 5. **External calls:** users (verify credentials).
- **Refresh Token / Password Reset:** **Not Implemented.** **Events:** Not Implemented.

### aadhar_service (:8005) — no DB
- **Purpose:** simulated UIDAI Aadhaar verification. **REST:** `GET/POST /api/v1/verify`, `GET /api/v1/valid-numbers`. **Providers:** n/a. **Note:** internal-key imported but **not applied** (endpoints unauthenticated). **Events:** Not Implemented.

### company_crv_service (:8006) — no DB
- **Purpose:** simulated company registration verification. **REST:** `GET/POST /api/v1/company/verify`, `GET /api/v1/company/valid-companies`. Internal-key not applied. **Events:** Not Implemented.

### central_payment_gateway_service (:8008) — no DB
- **Purpose:** simulated payment. **Internal APIs:** `POST /api/v1/payment/process|validate` (internal-key). **Events:** Not Implemented.

### notification_service (:8007) — JSON file store
- **Purpose:** send/list/read/clear notifications. **Internal APIs:** `POST /api/v1/notify/send`, `GET /notify/{id}`, `POST /notify/read/{id}`, `DELETE /notify/{id}` (internal-key). **Store:** `data/notifications.json`. **Events:** Not Implemented.

### central_gateway_service (:8000) — no DB
- **Purpose:** reverse-proxy API gateway. **Behaviour:** path→backend routing, CORS, rate-limit (off by default), correlation-id. **Discovery:** StaticResolver. **Events:** n/a.

### registry_service (:8010) — in-memory
- **Purpose:** service discovery (register/heartbeat/deregister/resolve/list/health). **Status: built but NOT wired at runtime** (services use static URLs). **Events:** n/a.

## 3. Business Capability Map
```
Accounts
├── Savings Account Creation
├── Current Account Creation
├── Get Account / List Accounts (+ type filter, pagination)
├── Account Summary (totals + privilege histogram)
├── Balance Inquiry
├── Update Account
├── Activate / Inactivate (Suspend) / Close
├── Verify PIN (with lockout)
├── [internal] Debit / Credit / Privilege / Active-check
├── Interest ................................. Not Implemented
├── Statement ................................ Not Implemented (only transaction logs in transactions svc)
└── Freeze/Unfreeze .......................... = Inactivate/Activate (no separate "freeze")

Transactions
├── Deposit
├── Withdraw
├── Transfer (NEFT/RTGS/IMPS/UPI; DTO also allows CHEQUE)
├── Transaction History (all / by-account / summary)
├── Transfer Limits (get / remaining / rules / check / update)
├── Transaction-log Analytics (summary / date-range / by-reference / file / retention)
├── Idempotency
├── Reverse / Refund .......................... Not Implemented
└── Beneficiaries ............................. Not Implemented

Identity
├── Login (JWT issue, throttle)
├── Verify Token
├── Logout (jti revocation)
├── User Registration (admin-created via users svc; self-register DISABLED in auth)
├── User Management (edit/view/list/activate/inactivate)
├── Roles (ADMIN/TELLER/MANAGER) + RBAC
├── Credential Verification / Role Validation (internal)
├── Refresh Token ............................. Not Implemented
├── Password Reset / Forgot ................... Not Implemented
└── Permissions (fine-grained) ............... Not Implemented (role-based only)

Aadhaar
├── Verification (GET/POST)
├── List Valid Numbers
└── Full KYC ................................. Not Implemented (simulated membership check)

Company (CRV)
├── Registration Verification (GET/POST)
├── List Valid Companies
└── Company Lookup/Details ................... Not Implemented (validity only)

Notification
├── Send (internal) / Get / Mark-read / Clear
├── Email / SMS / Push ....................... Not Implemented (in-app JSON store only)
└── Internal Notifications ................... Implemented (file-backed)

Payment
├── Process Payment (simulated) / Validate Transfer (simulated)

Gateway ── Reverse Proxy · CORS · Rate-limit(off) · Correlation-id
Registry ── Register/Heartbeat/Resolve (built, UNUSED at runtime)
```

## 4. Feature Inventory
(Owning svc · tables · repository · provider · **use case** · DTOs · **domain** · ORM · external · rules)

| Feature | Owner | Tables | Repository (impl) | UseCase | DTOs | Domain model | ORM | External | Key rules |
|---|---|---|---|---|---|---|---|---|---|
| Savings Account Creation | accounts | accounts, savings_account_details | `AccountRepository` (Entity Framework Core/InMemory) | **Not Impl** (logic in `AccountService.create_savings_account`→`SavingsImpl`) | SavingsAccountCreate→AccountResponse | **Not Impl** (anemic) | AccountORM, SavingsAccountDetailsORM | aadhar verify, notify | age≥18, aadhaar valid+unique(blind idx)+encrypted, not blacklisted, PIN bcrypt, balance≥2000 |
| Current Account Creation | accounts | accounts, current_account_details | `AccountRepository` | **Not Impl** (`AccountService`→`CurrentImpl`) | CurrentAccountCreate→AccountResponse | **Not Impl** | AccountORM, CurrentAccountDetailsORM | company verify, notify | registration valid+unique, PIN bcrypt, balance=0 |
| Get/List/Summary | accounts | (read) | `AccountRepository` | **Not Impl** | AccountResponse | Not Impl | AccountORM(+details) | — | RBAC ADMIN/TELLER/MANAGER; pagination |
| Balance Inquiry | accounts | (read) | `AccountRepository` | **Not Impl** | BalanceResponse | Not Impl | AccountORM | — | RBAC; currency=INR |
| Update Account | accounts | accounts/details | `AccountRepository` | **Not Impl** | AccountUpdate→dict | Not Impl | AccountORM(+details) | — | ADMIN/TELLER |
| Activate/Inactivate/Close | accounts | accounts | `AccountRepository` | **Not Impl** | — | Not Impl | AccountORM | — | ADMIN; state transitions |
| Verify PIN | accounts | (read) | `AccountRepository` | **Not Impl** | PinVerifyRequest | Not Impl | AccountORM | — | bcrypt; lockout→423 |
| Debit/Credit (internal) | accounts | accounts | `AccountRepository` | **Not Impl** | primitives | Not Impl | AccountORM | internal-key | amount>0; atomic UPDATE |
| Deposit | transactions | transaction_logging, idempotency_keys | `Transaction*Repository` (asyncpg/Entity Framework Core/InMemory) | **Not Impl** (`DepositService`) | DepositRequest→dict | Not Impl | TransactionLoggingORM, IdempotencyKeyORM | accounts credit (breaker), notify | min 1.00; idempotent |
| Withdraw | transactions | transaction_logging, idempotency_keys | same | **Not Impl** (`WithdrawService`) | WithdrawRequest→dict | Not Impl | same | accounts verify-pin+debit, notify | PIN; balance; idempotent |
| Transfer | transactions | fund_transfers, transaction_logging×2, idempotency_keys | `FundTransfer/Log/Limit/Idempotency Repository` | **Not Impl** (`TransferService`) | FundTransferCreate/TransferRequest→dict | Not Impl | FundTransferORM, TransactionLoggingORM | accounts debit+credit, payment, notify | PIN; within daily+per-txn limit; mode∈{NEFT,RTGS,IMPS,UPI}; idempotent; compensation |
| Transfer Limits | transactions | transfer_limits | `TransferLimitRepository` | **Not Impl** (`TransferLimitService`) | dict | Not Impl | TransferLimitORM | — | PREMIUM/GOLD/SILVER(/BASIC) daily+per-txn+count |
| Transaction History/Analytics | transactions | (read) | `TransactionLogRepository` | **Not Impl** (`TransactionLogService`) | dict | Not Impl | TransactionLoggingORM | — | RBAC; owner-or-staff |
| Login | auth | auth_tokens, auth_audit_logs | `AuthTokenRepository`, `AuthAuditRepository` | **Not Impl** (`AuthService.login`) | LoginRequest→TokenResponse | Not Impl | AuthTokenORM, AuthAuditLogORM | users verify | throttle/lockout; RS256/HS256; jti |
| Verify/Logout | auth | auth_tokens | `AuthTokenRepository` | **Not Impl** | dict | Not Impl | AuthTokenORM | — | jti revocation |
| Add/Edit/View/List/Activate User | users | users, user_audit_logs | `UserRepository`, `AuditRepository` | **Not Impl** (`*UserService`) | Add/Edit/View/List DTOs | Not Impl | UserORM, AuditLogORM | — | login_id unique; pwd≥8; RBAC |
| Verify Aadhaar | aadhar | none | **Not Impl** | **Not Impl** (`AadharVerificationService`) | AadharVerification req/resp | Not Impl | none | — | membership in dataset |
| Verify Company | company | none | **Not Impl** | **Not Impl** (`CompanyVerificationService`) | CompanyVerification req/resp | Not Impl | none | — | membership in dataset |
| Send/Get Notification | notification | notifications.json | **Not Impl** (JSON store) | **Not Impl** (`NotificationService`) | NotificationRequest | Not Impl | none | — | internal-key |
| Process/Validate Payment | payment | none | **Not Impl** | **Not Impl** (`PaymentGatewayService`) | Payment/Validation req/resp | Not Impl | none | — | internal-key; simulated |

## 5. REST API Inventory
Full endpoint list (≈60 routes, public + internal) with methods, models, status codes, auth/roles, exception behaviour is in [api-route-inventory.md](api-route-inventory.md). Summary: all user-facing routes under `/api/v1`; internal routes behind `X-Internal-API-Key`; creates return **201**; verify-pin lockout **423**; login throttle **429**.

## 6. Database Ownership Matrix
| Table | Owner | Aggregate (target) | Repository | Providers | Used by features | Referenced by |
|---|---|---|---|---|---|---|
| accounts | accounts | Account (Not Impl) | AccountRepository | all 5 | all account features | transactions (via API) |
| savings_account_details | accounts | Account | AccountRepository | all 5 | savings create/read | — |
| current_account_details | accounts | Account | AccountRepository | all 5 | current create/read | — |
| fund_transfers | transactions | Transfer (Not Impl) | FundTransferRepository | all 5 | transfer | — |
| transaction_logging | transactions | TransactionLog (Not Impl) | TransactionLogRepository | all 5 | deposit/withdraw/transfer/history | — |
| transfer_limits | transactions | TransferLimit (Not Impl) | TransferLimitRepository | all 5 | limits | — |
| idempotency_keys | transactions | Idempotency (Not Impl) | IdempotencyRepository | all 5 | deposit/withdraw/transfer | — |
| users | users | User (Not Impl) | UserRepository | all 5 | user mgmt | auth (via API) |
| user_audit_logs | users | — | AuditRepository | all 5 | audit | — |
| auth_tokens | auth | AuthToken (Not Impl) | AuthTokenRepository | all 5 | login/verify/logout | — |
| auth_audit_logs | auth | — | AuthAuditRepository | all 5 | auth audit | — |
| notifications.json | notification | — | none (file) | n/a | notifications | — |

**Database coupling between services:** **None.** No service reads/writes another's DB; `account_number`/`login_id`/`user_id` are cross-service **references** (no cross-DB FKs). FKs (CASCADE) exist only within accounts (details→accounts).

## 7. Repository Ownership Matrix
| Interface | Impls | Owner | Aggregate |
|---|---|---|---|
| `AccountRepository` | Entity Framework Core, InMemory | accounts | Account |
| `TransactionRepositoryInterface` | asyncpg, Entity Framework Core, InMemory | transactions | Transfer/Transaction |
| `TransactionLogRepositoryInterface` | asyncpg, Entity Framework Core, InMemory | transactions | TransactionLog |
| `TransferLimitRepositoryInterface` | asyncpg, Entity Framework Core, InMemory | transactions | TransferLimit |
| `IdempotencyRepositoryInterface` | asyncpg, Entity Framework Core, InMemory | transactions | Idempotency |
| `UserRepository`, `AuditRepository` | Entity Framework Core, InMemory | users | User |
| `AuthTokenRepository`, `AuthAuditRepository` | Entity Framework Core, InMemory | auth | AuthToken |
| aadhar/company/notification/payment | **Not Implemented** (stateless) | — | — |

## 8. Provider Matrix
accounts/transactions/users/auth support all 5 (inmemory/sqlite/mysql/postgres/supabase); transactions uses raw asyncpg for postgres/supabase. Others: no persistence. **Verified in tests: inmemory (all) + sqlite (accounts).** pgAdmin is not a provider. Detail: [provider-support-matrix.md](provider-support-matrix.md).

## 9. Domain Model Inventory
**Current: Not Implemented in any service** (anemic). The shared foundation `gdb_common.domain` (Entity, AggregateRoot, ValueObject, DomainEvent, DomainError, Specification, Result) exists but is unused by services. Target domain models per bounded context are listed in §7 (DDD) below.

## 10. Aggregate Inventory
**Current: Not Implemented.** Target aggregates: `Account` (accounts), `Transfer`/`TransactionLog`/`TransferLimit`/`Idempotency` (transactions), `User` (users), `AuthToken` (auth).

## 11. Value Object Inventory
**Current: Not Implemented.** Target VOs (grounded in existing validated fields): `Money`, `Aadhaar` (+masking/blind-index), `PhoneNumber`, `RegistrationNumber`, `AccountNumber`, `Privilege`, `Gender`, `DateOfBirth`, `Pin`, `TransferMode`, `Role`.

## 12. Repository Inventory
See §7 — interfaces + implementations **exist** for the 4 stateful services (the one fully-implemented tactical element). Stateless services: Not Implemented.

## 13. Port Inventory
**Current: Not Implemented** (no formal outbound ports). Cross-service calls use integration clients directly. Target ports: `VerificationPort` (Aadhaar/Company), `NotificationPort`, `PaymentPort`, `AccountBalancePort` (transactions→accounts), `CredentialPort` (auth→users).

## 14. Adapter Inventory
**Current: Not Implemented as adapters.** Existing integration **clients** (to be wrapped by future adapters): `AadharClient`, `CompanyClient`, `NotificationClient` (accounts); `AccountServiceClient` (circuit-breaker), `PaymentGatewayClient`, `NotificationClient` (transactions); `UserServiceClient` (auth).

## 15. DTO Inventory
**Implemented.** accounts: C# DTOs `@dataclass` (SavingsAccountCreate, CurrentAccountCreate, AccountResponse, SavingsAccountResponse, CurrentAccountResponse, BalanceResponse, AccountUpdate, Debit/CreditRequest, AccountDetailsResponse, ErrorResponse). transactions/auth: `@dataclass` (FundTransferCreate/Response, TransactionLoggingCreate/Response, TransferLimit*, LoginRequest, TokenResponse). users: C# DTOs requests + dataclass responses. aadhar/company/notification/payment: request/response models. Detail: [backward-compatibility-contracts.md](backward-compatibility-contracts.md).

## 16. ORM Inventory
**Implemented (4 services):** accounts (AccountORM, SavingsAccountDetailsORM, CurrentAccountDetailsORM); transactions (FundTransferORM, TransactionLoggingORM, TransferLimitORM, IdempotencyKeyORM); users (UserORM, AuditLogORM); auth (AuthTokenORM, AuthAuditLogORM). Others: Not Implemented. Schema detail: [database-schema-baseline.md](database-schema-baseline.md).

## 17. Microservice Communication Matrix
| Caller | Target | Reason | API | Method | Req→Resp | Auth | Timeout | Breaker | Retry | Future event |
|---|---|---|---|---|---|---|---|---|---|---|
| accounts | aadhar | savings KYC | /api/v1/verify | POST | {aadhar_number}→{is_valid,status} | internal-key | ~5s | ❌ | ❌ | AadhaarVerified |
| accounts | company_crv | current KYC | /api/v1/company/verify | POST | {registration_number}→{is_valid} | internal-key | ~5s | ❌ | ❌ | CompanyVerified |
| accounts | notification | welcome msg | /api/v1/notify/send | POST | NotificationRequest→{} | internal-key | ~2s | ❌ | ❌ | AccountOpened (async) |
| transactions | accounts | verify-pin/debit/credit | /api/v1/internal/accounts/... | GET/POST | primitives | internal-key | 10s | ✅ (5/30s) | ❌ | BalanceChanged |
| transactions | payment | validate/process | /api/v1/payment/... | POST | Payment/Validation | internal-key | ~2–5s | ❌ | ❌ | PaymentProcessed |
| transactions | notification | txn msg | /api/v1/notify/send | POST | NotificationRequest→{} | internal-key | ~2s | ❌ | ❌ | FundsTransferred (async) |
| auth | users | verify credentials | /internal/v1/users/verify | POST | {login_id,password}→{valid,role} | internal-key | ~2–5s | ❌ | ❌ | — |
| gateway | all | reverse proxy | /{service}/{path} | any | passthrough | forwards Bearer | 30s | ❌ | ❌ | — |

Rules (current): sync HTTP; retry not implemented anywhere; breaker only transactions→accounts; all forward `X-Correlation-ID`. Integration-client detail: [business-capability-report.md §9](business-capability-report.md).

## 18. End-to-End Sequence Diagrams
> Diagrams depict the **current anemic flow** (grounded). The target flow (Use Case → Port → Adapter) is **Not Implemented**; the future mapping is noted per diagram.

**Savings Account Creation**
```
Client → Gateway → accounts POST /api/v1/accounts/savings (RBAC ADMIN/TELLER)
  → AccountService.create_savings_account → SavingsImpl (age≥18, blacklist)
  → AadharClient.verify_aadhar → aadhar :8005 /api/v1/verify → {is_valid}
  → EncryptionManager: bcrypt PIN, Fernet-encrypt Aadhaar + blind index
  → AccountRepository.create_savings_account → accounts + savings_account_details (commit)
  → NotificationClient.send (best-effort) → 201 AccountResponse (Aadhaar masked)
  [future: UseCase → VerificationPort → AadhaarAdapter → HTTP; UoW commit; Mapper→DTO]
```
**Current Account Creation**
```
Client → Gateway → accounts POST /accounts/current → AccountService→CurrentImpl
  → CompanyClient.verify_registration → company :8006 → {is_valid}
  → AccountRepository.create_current_account → accounts + current_account_details (commit) → notify → 201
  [future: UseCase → VerificationPort → CompanyAdapter → HTTP]
```
**Deposit**
```
Client → Gateway → transactions POST /deposits (RBAC MANAGER/TELLER, Idempotency-Key)
  → DepositService → Idempotency.reserve → AccountServiceClient.credit (breaker) → accounts :8001
  → TransactionLogRepository.log → NotificationClient.send → Idempotency.complete → 201
```
**Withdraw**
```
Client → Gateway → transactions POST /withdrawals → WithdrawService → Idempotency.reserve
  → AccountServiceClient.verify-pin + balance → accounts.debit (breaker) → log → notify → 201
```
**Transfer**
```
Client → Gateway → transactions POST /transfers → TransferService → Idempotency.reserve
  → verify-pin + privilege → TransferLimit check (daily+per-txn) → mode/amount validation
  → accounts.debit(from) → accounts.credit(to)  [NO distributed txn; manual compensation]
  → fund_transfers + 2× transaction_logging → notify → Idempotency.complete → 201
```
**Login**
```
Client → Gateway → auth POST /api/v1/auth/login → AuthService.login → throttle check
  → UserServiceClient.verify → users /internal/v1/users/verify → issue JWT (jti→auth_tokens) → 200 TokenResponse
```
**Verify Token** `Gateway → auth GET /auth/verify → JWTValidator (RS256/HS256) → 200 claims`
**Logout** `Gateway → auth POST /auth/logout → mark jti revoked → 200`
**User Registration (admin)** `Gateway → users POST /users (ADMIN) → add_user (login_id unique, pwd≥8) → users + audit → 201` *(public self-register: DISABLED → 403)*
**Verify Aadhaar** `Gateway → aadhar GET/POST /api/v1/verify → membership check → {is_valid,status}`
**Verify Company** `Gateway → company GET/POST /api/v1/company/verify → membership check → {is_valid,status}`
**Balance Inquiry** `Gateway → accounts GET /accounts/{n}/balance → AccountService.get_balance → repo → BalanceResponse`
**Notification** `accounts/transactions → notification POST /notify/send (internal-key, fire-and-forget)`
**Password Reset / Refresh Token:** **Not Implemented** (no such endpoints/flows).

## 19. Dependency Matrix
| Service | Depends on | Direction | Reason | Type |
|---|---|---|---|---|
| accounts | aadhar, company_crv, notification | out | KYC + notify | sync (notify → future async) |
| transactions | accounts, payment, notification | out | balance mutation, payment, notify | sync (breaker on accounts; notify → future async) |
| auth | users | out | credential verification | sync |
| users | — | — | — | — |
| gateway | all | out | proxy | sync |
| aadhar/company/payment/notification | — | in | called by accounts/transactions | sync |
| registry | — | — | unused at runtime | — |
No cyclic dependencies. Future event candidates: notifications, account-opened, funds-transferred, balance-changed (move off the sync path).

## 20. Business Rules Inventory
- **Savings:** age ≥ 18; initial_balance ≥ 2000; Aadhaar valid (verified) + unique (blind index) + encrypted at rest; applicant not blacklisted; PIN bcrypt-hashed.
- **Current:** company registration valid + unique; opening balance = 0.00; PIN bcrypt.
- **Account lifecycle:** activate/inactivate/close state transitions (ADMIN); verify-PIN lockout after max attempts (→423).
- **Money:** deposit/withdraw/transfer min 1.00; max 999,999,999.99; withdraw/transfer require PIN + sufficient balance; transfer within privilege daily limit + per-txn limit + daily count (PREMIUM 100k/50k/50, GOLD 50k/25k/25, SILVER 25k/12.5k/10, BASIC fallback 10k/10k/5); transfer mode ∈ {NEFT,RTGS,IMPS,UPI} (DTO also accepts CHEQUE — DB rejects on postgres); all money ops idempotent (Idempotency-Key).
- **Identity:** login throttle/lockout; login_id unique; password ≥ 8; roles ADMIN/TELLER/MANAGER; JWT jti revocable; self-registration disabled.
- **Aadhaar/Company:** validity = membership in the simulated dataset.

## 21. Validation Rules Inventory
- **Format (DTO/C# DTOs):** name 2–255; pin exactly 4 digits; phone_no exactly 10 digits; aadhar_number exactly 12 digits; date_of_birth `YYYY-MM-DD`; gender ∈ {Male,Female,Others}; privilege ∈ {PREMIUM,GOLD,SILVER}; registration_no 1–50; website ≤255; login_id 3–50 `^[a-zA-Z0-9._-]+$`; password ≥ 8; transfer_amount > 0 (2 dp).
- **Business (service layer, → future Domain):** age, min-balance, uniqueness, limits, blacklist, PIN verify — see §20.
- **DB constraints:** unique (`account_number`, `aadhar_hash`, `registration_no`, `users.login_id`, `auth_tokens.token_jti`); natural PKs (`transfer_limits.privilege`, `idempotency_keys.idempotency_key`); CASCADE FKs within accounts; CHECK constraints only on transactions-postgres.

## 22. Security Rules Inventory
- **AuthN:** JWT `Authorization: Bearer` (RS256 if key set, else HS256); tokens issued by auth; jti revocation.
- **AuthZ:** RBAC ADMIN/TELLER/MANAGER via `require_*` deps; owner-or-admin checks for view-self / verify-pin.
- **Internal:** `X-Internal-API-Key` (constant-time compare) on internal routers. ⚠️ aadhar/company import but **do not apply** it → their verify endpoints are unauthenticated.
- **Crypto:** bcrypt (12 rounds) for PIN/password; Fernet encryption + HMAC blind index for Aadhaar; Aadhaar masked in responses.
- **Hardening (Phase 1):** safe error envelope (no `str(exc)`); security headers + CSP; CORS allow-list at gateway; production secret guards (incl. PIN/SECRET keys); `create_all` off in production.
- **Gaps/risks:** live Supabase credential to rotate (R-01); rate-limit off by default; bcrypt on event loop; unauthenticated aadhar/company verify.

## 23. Feature Complexity Report
| Feature | Complexity | Migration | Business risk | Regression risk | Suggested order |
|---|---|---|---|---|---|
| Balance Inquiry / Get / Summary | Low | Low | Low | Low | 1 (read-only warm-up) |
| Savings Account Creation | Medium | Medium | Medium | Low (213 tests) | 2 (reference slice) |
| Current Account Creation | Medium | Medium | Medium | Low | 3 (same aggregate) |
| Update / Activate / Inactivate / Close | Low-Med | Low | Medium | Low | 4 |
| Verify PIN (+ internal debit/credit) | Medium | Medium | High (money) | Medium | 5 (needed by money movement) |
| Aadhaar / Company Verify (as ports) | Low | Low | Low | Low | with 2/3 (port-only) |
| Login / Verify / Logout | Medium | Medium | High (security) | Medium | 6 |
| User Management | Medium | Medium | Medium | Low | 7 |
| Deposit | High | High | High | Medium | 8 |
| Withdraw | High | High | High | Medium | 9 |
| Transfer + Limits | Critical | Critical | Critical | High | 10 (needs UoW + Saga) |
| Transaction History/Analytics | Medium | Medium | Low | Low | 11 |
| Notifications (as async) | Medium | Medium | Low | Low | later (event phase) |

## 24. Recommended Migration Order (feature-by-feature)
1. **Balance Inquiry + Get/Summary (accounts, read-only)** — warm up the stack (domain read model, mapper, repo, UoW, composition root) with the lowest risk.
2. **Savings Account Creation** — the reference vertical slice: `Account` aggregate + `Money`/`Aadhaar` VOs, Aadhaar **port**, mapper, UoW; self-contained (no distributed txn); 213 tests guard it.
3. **Current Account Creation** — same aggregate/repo; adds the Company **port**. Completes the Accounts write side.
4. **Update / Activate / Inactivate / Close** — finish the Account lifecycle behaviours on the aggregate.
5. **Verify PIN + internal Debit/Credit** — harden the account-money surface (still intra-service) that money movement depends on.
6. **Login / Verify / Logout (auth)** — identity behind the frozen contract; introduces the `CredentialPort` to users.
7. **User Management (users)** — completes Identity.
8. **Deposit** → 9. **Withdraw** — money movement, single counterparty; introduces idempotency + accounts port under a UoW.
10. **Transfer + Transfer Limits** — last and hardest: distributed debit→credit needs UoW **and Saga/compensation**.
11. **Transaction History/Analytics** — read side of transactions.
Then **Notifications/events** as the async decoupling phase.

**Justification:** ordered by (a) read-before-write, (b) self-contained-before-distributed, (c) dependency readiness (money movement needs accounts debit/credit first), (d) risk (Transfer last). Aadhaar/Company/Payment/Notification are migrated only as **ports/adapters**, never as aggregates.

## 25. Risks
Distributed money movement (Critical), provider divergence incl. CHEQUE mode (High), unverified non-sqlite providers (Medium), ~60 frozen route/DTO contracts + Aadhaar masking + internal debit/credit (Medium), anemic coupling / DI self-resolution + module singletons (Medium), unauthenticated aadhar/company verify (Medium), operational: rotate Supabase credential (R-01), gates not in CI (Medium).

## 26. Readiness Assessment
**Ready to begin feature migration.** Standard frozen (Phase 0.5 + standardization), foundation shipped (Phase 2 `gdb_common`), baseline green (686 tests, OpenAPI 10/10, 64 architecture tests). No tactical-DDD code exists in services yet — expected; that is exactly what the migration adds. Blockers are operational only (rotate credential; wire gates into CI).

## 27. Recommendation for the Next Phase
Execute **Feature 1–2 of the migration order** — Accounts read model + **Savings Account Creation** — as the first implemented vertical slice against the frozen standard: build the `Account` aggregate, `Money`/`Aadhaar` VOs, `VerificationPort`+`AadhaarAdapter`, `AccountMapper`, `Entity Framework CoreUnitOfWork`+repository, and an Accounts `CompositionRoot`, strictly behind the unchanged API and the OpenAPI/architecture gates; extend the architecture tests to scan the accounts `domain/`/`application/` in the same PR; adopt EF Core Migrations-as-truth for accounts. Then proceed feature-by-feature per §24.



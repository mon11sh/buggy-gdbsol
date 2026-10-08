# GDB Implementation Blueprint (Feature Migration Plan)

**Purpose:** the concrete, feature-by-feature plan to migrate every existing feature into the frozen enterprise architecture. **Not** an architecture doc — an execution guide. Planning only; no code changed.
**Grounding:** every "current" fact is from source; every target component not present today is marked **Not Implemented / TO CREATE**. Companion refs: [enterprise-microservice-standard](../architecture/enterprise-microservice-standard.md), [migration-template](../architecture/migration-template.md), [developer-checklist](../architecture/developer-checklist.md), [business-architecture](business-architecture.md).

> **Baseline reality:** No service implements domain models, aggregates, value objects, ports, adapters, use cases, mappers, unit-of-work, or domain events (verified). **Present today:** DTOs, ORM, repository interfaces + implementations, integration HTTP clients, service classes (business logic), and the unused `gdb_common` tactical contracts. Every migration below = wrap existing behaviour in the standard layers **behind the frozen API**.

---

## 1. Executive Summary
Migration is organized **feature-by-feature**, not service-by-service, in 7 batches. Batch 1 lays per-service foundation (composition root, UoW, mapper, EF Core Migrations-as-truth, gate CI). Accounts is the reference batch (self-contained, 213 tests). Identity follows; Money Movement is last and hardest (distributed debit→credit → needs UoW + Saga). Aadhaar/Company/Payment/Notification are migrated **only as ports+adapters**, never as aggregates. Every feature has a fixed 18-point checklist and must keep the OpenAPI + architecture gates green. Estimated ~30 migratable features across 4 stateful services; ~4 stateless services become adapters.

## 2. Microservice Inventory (migration relevance)
| Service | Migrate as | Aggregates to build | Repo iface today | Notes |
|---|---|---|---|---|
| accounts | full stack | Account | ✅ AccountRepository | reference batch; 213 tests |
| transactions | full stack | Transfer, TransactionLog, TransferLimit, Idempotency | ✅ 4 ifaces (×3 impls) | last; UoW + Saga |
| users | full stack | User | ✅ User/Audit | identity |
| auth | full stack | AuthToken | ✅ AuthToken/Audit | identity; RS256/HS256 |
| aadhar | **port+adapter only** | — | Not Impl | VerificationPort/AadhaarAdapter |
| company_crv | **port+adapter only** | — | Not Impl | VerificationPort/CompanyAdapter |
| payment | **port+adapter only** | — | Not Impl | PaymentPort/PaymentAdapter |
| notification | **port+adapter only** | — | Not Impl | NotificationPort/NotificationAdapter |
| gateway | leave (infra) | — | — | no aggregate |
| registry | leave (infra, unused) | — | — | no aggregate |

## 3. Feature Inventory (with current stack — grounded)
| ID | Feature | Svc | Endpoint | Req DTO | Resp DTO | Current service class | Current repo | ORM | External calls | Provider |
|---|---|---|---|---|---|---|---|---|---|---|
| F1 | Balance Inquiry | accounts | GET /accounts/{n}/balance | path | BalanceResponse | AccountService.get_balance | AccountRepository | AccountORM | — | all5 |
| F2 | Get Account | accounts | GET /accounts/{n} | path | dict | AccountService.get_account_details | AccountRepository | AccountORM(+details) | — | all5 |
| F3 | List Accounts | accounts | GET /accounts | query | list[AccountResponse] | AccountService.get_all_accounts | AccountRepository | AccountORM | — | all5 |
| F4 | Account Summary | accounts | GET /accounts/summary | — | dict | AccountService.get_accounts_summary | AccountRepository | AccountORM | — | all5 |
| F5 | Savings Creation | accounts | POST /accounts/savings | SavingsAccountCreate | AccountResponse | AccountService.create_savings_account→SavingsImpl | AccountRepository | AccountORM+SavingsDetailsORM | aadhar, notification | all5 |
| F6 | Current Creation | accounts | POST /accounts/current | CurrentAccountCreate | AccountResponse | AccountService.create_current_account→CurrentImpl | AccountRepository | AccountORM+CurrentDetailsORM | company, notification | all5 |
| F7 | Update Account | accounts | PUT /accounts/{n} | AccountUpdate | dict | AccountService.update_account | AccountRepository | AccountORM(+details) | — | all5 |
| F8-10 | Activate/Inactivate/Close | accounts | POST /accounts/{n}/{action} | path | dict | AccountService.activate/inactivate/close_account | AccountRepository | AccountORM | — | all5 |
| F11 | Verify PIN | accounts | POST /accounts/{n}/verify-pin | PinVerifyRequest | dict | AccountService.verify_pin | AccountRepository | AccountORM | — | all5 |
| F12-13 | Internal Debit/Credit | accounts | POST /internal/accounts/{n}/debit\|credit | primitives | dict | InternalAccountService | AccountRepository | AccountORM | — | all5 |
| F14 | Deposit | transactions | POST /deposits | primitives/DepositRequest | dict | DepositService.process_deposit | TransactionLog+Idempotency | TransactionLoggingORM,IdempotencyKeyORM | accounts(credit,breaker), notification | all5 |
| F15 | Withdraw | transactions | POST /withdrawals | primitives/WithdrawRequest | dict | WithdrawService.process_withdraw | TransactionLog+Idempotency | same | accounts(verify-pin,debit), notification | all5 |
| F16 | Transfer | transactions | POST /transfers | FundTransferCreate/TransferRequest | dict | TransferService.process_transfer | Transaction,Log,TransferLimit,Idempotency | FundTransferORM,TransactionLoggingORM | accounts(debit,credit), payment, notification | all5 |
| F17 | Transfer Limits | transactions | GET/POST/PUT /transfer-limits/* | dict | dict | TransferLimitService | TransferLimitRepository | TransferLimitORM | — | all5 |
| F18 | Txn History/Analytics | transactions | GET /transaction-logs/* | query | dict | TransactionLogService | TransactionLogRepository | TransactionLoggingORM | — | all5 |
| F19 | Login | auth | POST /auth/login | LoginRequest | TokenResponse | AuthService.login | AuthToken,AuthAudit | AuthTokenORM,AuthAuditLogORM | users(verify) | all5 |
| F20 | Verify Token | auth | GET /auth/verify | header | dict | AuthService.verify_token | AuthTokenRepository | AuthTokenORM | — | all5 |
| F21 | Logout | auth | POST /auth/logout | header | dict | AuthService.logout | AuthTokenRepository | AuthTokenORM | — | all5 |
| F22-26 | User Add/Edit/View/List/Activate | users | /users* | Add/Edit DTOs | *Response | *UserService | UserRepository,AuditRepository | UserORM,AuditLogORM | — | all5 |
| F27 | Verify Aadhaar | aadhar | /api/v1/verify | AadharVerification req | resp | AadharVerificationService | Not Impl | none | — | n/a |
| F28 | Verify Company | company | /company/verify | CompanyVerification req | resp | CompanyVerificationService | Not Impl | none | — | n/a |
| F29 | Notification | notification | /notify/* | NotificationRequest | dict | NotificationService+Storage | Not Impl (JSON) | none | — | n/a |
| F30 | Payment | payment | /payment/* | Payment/Validation req | resp | PaymentGatewayService | Not Impl | none | — | n/a |

Refresh Token, Password Reset, Statement, Interest, Reverse/Refund, Beneficiaries, Email/SMS/Push: **Not Implemented** — out of migration scope (nothing to migrate).

## 4. Feature → API Mapping
Endpoints/methods/status/auth per feature are frozen (ADR-009) — full list in [api-route-inventory](api-route-inventory.md). Migration keeps each identical; the OpenAPI snapshot gate enforces it.

## 5. Feature → Domain Mapping (target; all TO CREATE)
| Feature(s) | Aggregate | Entities | Domain services / policies |
|---|---|---|---|
| F1–F13 | **Account** | Account (root) | BlacklistPolicy, PinPolicy, AgePolicy, MinBalancePolicy |
| F14–F16 | **Transfer** / **TransactionLog** | Transfer, TransactionLog | TransferLimitPolicy, IdempotencyPolicy, CompensationPolicy |
| F17 | **TransferLimit** | TransferLimit | LimitRulePolicy |
| F18 | (read model) | — | — |
| F19–F21 | **AuthToken** | AuthToken | LoginThrottlePolicy, TokenRevocationPolicy |
| F22–F26 | **User** | User | RolePolicy, UniqueLoginPolicy |
All domain models are **Not Implemented** today; each is created during its feature's migration.

## 6. Feature → Repository Mapping
| Feature(s) | Interface (exists) | Implementations (exist) | Change needed |
|---|---|---|---|
| F1–F13 | AccountRepository ✅ | Entity Framework Core, InMemory ✅ | make it return **domain Account** (currently dict) + take UoW session |
| F14–F16 | Transaction/Log/TransferLimit/Idempotency ✅ | asyncpg, Entity Framework Core, InMemory ✅ | return domain objects; share UoW; unify asyncpg behind UoW |
| F17 | TransferLimitRepository ✅ | ✅ | return domain |
| F18 | TransactionLogRepository ✅ | ✅ | return domain (read) |
| F19–F21 | AuthTokenRepository ✅ | ✅ | return domain |
| F22–F26 | UserRepository/AuditRepository ✅ | ✅ | return domain |
Repository **interfaces + impls exist** — the one tactical element already present. Migration adapts them to domain-returning + UoW-session, and adds the missing InMemory-UoW binding.

## 7. Feature → Database Mapping
Tables per feature are in §3 and [database-schema-baseline](database-schema-baseline.md). **No schema change** during migration; EF Core Migrations becomes the authoritative source (ADR-008) starting with accounts.

## 8. Feature → Provider Mapping
All stateful features support inmemory/sqlite/mysql/postgres/supabase (transactions uses asyncpg for pg/supabase). Provider selection moves into each service's **CompositionRoot** via `ProviderRegistry` (TO CREATE). See [provider-strategy](../architecture/provider-strategy.md).

## 9. Feature → Port Mapping (target; all TO CREATE)
| Feature | Port | Direction |
|---|---|---|
| F5 Savings | VerificationPort (Aadhaar) | accounts→aadhar |
| F6 Current | VerificationPort (Company) | accounts→company |
| F5/F6/F14–F16 | NotificationPort | →notification |
| F14–F16 | AccountBalancePort (verify-pin/debit/credit) | transactions→accounts |
| F16 | PaymentPort | transactions→payment |
| F19 | CredentialPort | auth→users |
No formal ports exist today (direct integration clients). Each is created with its feature.

## 10. Feature → Adapter Mapping (target; all TO CREATE, wrap existing clients)
| Port | Adapter | Wraps existing client |
|---|---|---|
| VerificationPort(Aadhaar) | AadhaarAdapter | AadharClient.verify_aadhar |
| VerificationPort(Company) | CompanyAdapter | CompanyClient.verify_registration |
| NotificationPort | NotificationAdapter | NotificationClient.send |
| AccountBalancePort | AccountServiceAdapter | AccountServiceClient (breaker) |
| PaymentPort | PaymentAdapter | PaymentGatewayClient |
| CredentialPort | UserCredentialAdapter | UserServiceClient |

## 11. Feature → Use Case Mapping (target; all TO CREATE)
| Feature | Use Case | Command/Query |
|---|---|---|
| F1 | GetBalanceUseCase | GetBalanceQuery |
| F2/F3/F4 | GetAccountUseCase / ListAccountsUseCase / AccountSummaryUseCase | queries |
| F5 | CreateSavingsAccountUseCase | CreateSavingsAccountCommand |
| F6 | CreateCurrentAccountUseCase | CreateCurrentAccountCommand |
| F7 | UpdateAccountUseCase | UpdateAccountCommand |
| F8-10 | Activate/Inactivate/Close AccountUseCase | commands |
| F11 | VerifyPinUseCase | command |
| F12-13 | Debit/CreditAccountUseCase | commands |
| F14/F15/F16 | Deposit/Withdraw/TransferUseCase (CQRS commands exist as messages) | Deposit/Withdraw/TransferCommand |
| F17 | Get/Check/UpdateTransferLimitUseCase | cmd/query |
| F18 | GetTransactionsUseCase | queries |
| F19/F20/F21 | Login/VerifyToken/LogoutUseCase | commands |
| F22-26 | Add/Edit/View/List/SetUserStatusUseCase | cmd/query |
(transactions already has CQRS `messages.py`/`buses.py` — reuse, don't redesign.)

## 12. Feature → Aggregate Mapping — see §5 (Account, Transfer, TransactionLog, TransferLimit, AuthToken, User). All TO CREATE.

## 13. Feature → Value Object Mapping (target; all TO CREATE, from existing validated fields)
Account: Money, Aadhaar, PhoneNumber, RegistrationNumber, AccountNumber, Privilege, Gender, DateOfBirth, Pin. Transactions: Money, TransferMode, IdempotencyKey, PrivilegeLimit. Auth: Jti, Role, TokenExpiry. Users: LoginId, Role, PasswordHash.

## 14. Feature → External Service Mapping
F5→aadhar; F6→company; F5/F6/F14–F16→notification; F14–F16→accounts(internal, breaker); F16→payment; F19→users. All via future ports (§9/§10). Full comms detail in [business-architecture §17](business-architecture.md).

## 15. Feature → Sequence Diagram
Current (grounded) + future (Use Case→Port→Adapter) sequences for every feature are in [business-architecture §18](business-architecture.md). Target flow per feature = the mandatory [request-flow](../architecture/request-flow.md).

## 16. Feature Dependency Graph
```
Foundation (composition root, UoW, mapper, EF Core Migrations, gates-in-CI)
  └─► F1 Balance ─► F2/F3/F4 reads
        └─► F5 Savings ─► F6 Current ─► F7 Update ─► F8-10 Lifecycle
              └─► F11 Verify PIN ─► F12/F13 Debit/Credit  (money surface)
                    └─► F19 Login ─► F20/F21 ─► F22-26 Users (Identity, parallel-capable)
                          └─► F14 Deposit ─► F15 Withdraw ─► F16 Transfer + F17 Limits (needs UoW+Saga)
                                └─► F18 History
Ports (F27 Aadhaar, F28 Company, F29 Notification, F30 Payment) implemented WITH their consuming feature.
```
| Feature | Depends on | Reason | Must precede |
|---|---|---|---|
| F5 Savings | Foundation, VerificationPort(Aadhaar), F1 reads | reference slice; needs read model + KYC port | F6, all writes |
| F6 Current | F5, VerificationPort(Company) | same aggregate + company port | lifecycle |
| F11/F12/F13 | Account aggregate (F5) | account money surface | F14–F16 |
| F14 Deposit | F12/F13 (accounts credit/debit port), UoW | needs account balance port | F15, F16 |
| F16 Transfer | F14/F15, UoW + Saga | distributed debit→credit | F18 |
| F19 Login | F22-26 (users verify) or CredentialPort | credential verification | app auth |

## 17. Feature Complexity Matrix
| Feature | Difficulty | LOC est. | Repos | DTOs | ORM | Use cases | Tests est. |
|---|---|---|---|---|---|---|---|
| F1–F4 reads | Low | 300–500 | 1 (adapt) | reuse | reuse | 3–4 | 15–25 |
| F5 Savings | Medium | 600–900 | 1 | reuse | reuse | 1 | 25–35 |
| F6 Current | Medium | 300–450 | 1 | reuse | reuse | 1 | 15–20 |
| F7–F10 lifecycle | Low-Med | 300–500 | 1 | reuse | reuse | 4 | 15–25 |
| F11–F13 pin/debit/credit | Medium | 350–500 | 1 | reuse | reuse | 3 | 20–30 |
| F19–F21 auth | Medium | 500–700 | 2 | reuse | reuse | 3 | 20–30 |
| F22–F26 users | Medium | 500–800 | 2 | reuse | reuse | 5 | 25–35 |
| F14 Deposit | High | 500–700 | 2 | reuse | reuse | 1 | 25–35 |
| F15 Withdraw | High | 500–700 | 2 | reuse | reuse | 1 | 25–35 |
| F16 Transfer + F17 | Critical | 900–1400 | 4 | reuse | reuse | 3 | 40–60 |
| F18 history | Medium | 300–500 | 1 | reuse | reuse | 2 | 15–20 |
| F27–F30 ports | Low each | 150–250 each | 0 | reuse | none | 0 (adapters) | 8–12 each |
"DTOs/ORM: reuse" = frozen contract + existing schema unchanged.

## 18. Feature Risk Matrix
| Feature | Migration risk | Business risk | Regression risk | Mitigation |
|---|---|---|---|---|
| F1–F4 reads | Low | Low | Low | OpenAPI + existing tests |
| F5/F6 create | Medium | Medium | Low (213 tests) | KYC port fakes; masking/encryption mapper tests |
| F11–F13 | Medium | High (money) | Medium | keep internal API identical; contract tests |
| F19–F21 | Medium | High (security) | Medium | RS256/HS256 parity; jti revocation tests |
| F14/F15 | High | High | Medium | idempotency + breaker preserved; fault-injection |
| F16+F17 | Critical | Critical | High | UoW + Saga/compensation; cross-provider tests; do last |
| F27–F30 ports | Low | Low | Low | adapter wraps existing client; behaviour unchanged |

## 19. Recommended Implementation Batches
- **Batch 1 — Foundation:** per-service `CompositionRoot` + `ProviderRegistry` wiring, `Entity Framework CoreUnitOfWork` + `InMemoryUnitOfWork`, `AccountMapper` scaffold, extend architecture tests to scan per-service `domain/`/`application/`, adopt **EF Core Migrations-as-truth for accounts**, wire OpenAPI + architecture gates into CI. (No feature behaviour yet.)
- **Batch 2 — Accounts:** F1–F4 reads → F5 Savings → F6 Current → F7–F10 lifecycle → F11 Verify PIN → F12/F13 Debit/Credit. Includes Aadhaar (F27) + Company (F28) as ports/adapters.
- **Batch 3 — Identity:** F19–F21 (auth) + F22–F26 (users), with CredentialPort.
- **Batch 4 — Transactions:** F14 Deposit → F15 Withdraw → F16 Transfer + F17 Limits (UoW + Saga) → F18 History; AccountBalancePort.
- **Batch 5 — Notifications:** NotificationPort/Adapter + move notifications off the sync path (event/outbox groundwork).
- **Batch 6 — Payments:** PaymentPort/Adapter (consumed by F16).
- **Batch 7 — Remaining:** analytics polish, gateway/registry left as infra, cross-provider verification, retire superseded service/repo code.

## 20. Recommended Feature Migration Order (justified)
1. **F1 Balance** — read-only warm-up: proves domain read model + mapper + repo + UoW + composition root at minimal risk.
2. **F2/F3/F4 reads** — complete the read side; reuse the same wiring.
3. **F5 Savings Creation** — reference write slice; self-contained aggregate, no distributed txn, 213 tests guard it; introduces Aadhaar port.
4. **F6 Current Creation** — same aggregate/repo; adds Company port.
5. **F7–F10 lifecycle** — finish Account behaviours on the aggregate.
6. **F11 Verify PIN + F12/F13 Debit/Credit** — harden the account money surface money-movement depends on (still intra-service).
7. **F19–F21 Login/Verify/Logout** — identity behind frozen contract; CredentialPort.
8. **F22–F26 User management** — completes Identity.
9. **F14 Deposit** → **10. F15 Withdraw** — money movement, single counterparty; idempotency + accounts port under UoW.
11. **F16 Transfer + F17 Limits** — last & hardest: distributed debit→credit needs UoW + Saga/compensation.
12. **F18 Transaction History** — read side of transactions.
Then **Notifications/Payments as ports + async** (Batches 5–6). Order rationale: read-before-write, self-contained-before-distributed, dependency-readiness, risk-last.

## 21. Missing Components (blockers per feature)
**Universal (every stateful feature):** Domain models/aggregates/VOs — Not Impl; Use Cases/Commands/Queries — Not Impl; Ports/Adapters — Not Impl; Mappers (DtoMapper + ORM Mapper) — Not Impl; Unit-of-Work impl — Not Impl; per-service Composition Root wiring — Not Impl; per-service architecture tests — Not Impl (shared gate exists).
**Present (not blockers):** Repository interfaces + implementations ✅; DTOs ✅; ORM ✅; Providers ✅; integration HTTP clients ✅; `gdb_common` tactical contracts ✅; OpenAPI gate ✅; shared architecture test harness ✅.
**Feature-specific blockers:**
| Feature | Key missing before it can migrate |
|---|---|
| F5 | VerificationPort+AadhaarAdapter; Account aggregate; AccountMapper (encrypt/mask); AccountsCompositionRoot; Entity Framework CoreUnitOfWork |
| F6 | CompanyAdapter (+ above) |
| F14–F16 | AccountBalancePort+adapter; UoW spanning repos; (F16) Saga/compensation; unify asyncpg behind UoW |
| F19 | CredentialPort+adapter; AuthToken aggregate |
| F16 | Distributed-transaction story (UoW gives intra-service only) — **the one architectural gap** |
**Missing providers:** none (all 5 exist) — but MySQL/Postgres/Supabase are **unverified** (only inmemory + accounts-sqlite tested).

## 22. Readiness Assessment
**Ready to start Batch 1 → Batch 2.** Frozen standard + shipped `gdb_common` contracts + green baseline (686 tests, OpenAPI 10/10, 64 architecture tests) + repository interfaces already present. The only true design gap is a **distributed-transaction (Saga) pattern for F16**, which is deferred to Batch 4. Operational pre-work: rotate Supabase credential (R-01); wire gates into CI; adopt EF Core Migrations-as-truth for accounts.

## 23. Recommendation for Phase 4
Execute **Batch 1 (Foundation)** then **Batch 2 features F1→F5→F6** as the first implementation increment: build the Accounts read model + Savings/Current creation against the standard (`Account` aggregate, `Money`/`Aadhaar` VOs, `VerificationPort`+`AadhaarAdapter`/`CompanyAdapter`, `AccountMapper`, `Entity Framework CoreUnitOfWork`+domain-returning repository, `AccountsCompositionRoot`), strictly behind the frozen API, with the OpenAPI + (extended) architecture gates green and EF Core Migrations authoritative for accounts. Proceed feature-by-feature per §20; keep Transfer (F16) last with a Saga design.



# GDB .NET — Concepts Navigator (curriculum → code, with line numbers)

One page to walk a reviewer from `if / else` to enterprise patterns, each concept pinned to the exact file and
line where it lives in this solution. Every link is `path#L<line>`; on GitHub/GitLab it opens the file at that
line, in VS Code `Ctrl+click` does the same. Line numbers are as of commit `6591e15` (branch `solution-adonet`).

**Stack:** .NET 10 · ASP.NET Core · EF Core 10 (PostgreSQL/SQL Server/MySQL/SQLite/in-memory) · ADO.NET twin
data path · React front end · Docker · GitHub Actions. **Quality bar:** 0 warnings (warnings are errors),
136 tests (120 unit + 16 integration), 10 OpenAPI contracts under a drift gate.

---

## A. Language fundamentals

| # | Concept | Where | What to look at |
|---|---|---|---|
| 1 | `if` / `else` guards | [Account.cs#L57](AccountsService/Domain/Models/Account.cs#L57) · [Account.cs#L142](AccountsService/Domain/Models/Account.cs#L142) | `Debit` rejects overdraft; `RequireOperable` checks CLOSED before "not ACTIVE" (order matters) |
| 2 | `switch` statement | [AccountMapper.cs#L30](AccountsService/Mapping/AccountMapper.cs#L30) · [Program.cs#L75](AccountsService/Program.cs#L75) | status mapping; database provider selection |
| 3 | `switch` expression + type patterns | [AccountResponseMapper.cs#L32](AccountsService/Mapping/AccountResponseMapper.cs#L32) | `account switch { SavingsAccount s => …, CurrentAccount c => … }` |
| 4 | `for` loop | [AccountRepository.cs#L95](AccountsService/Infrastructure/Repositories/AccountRepository.cs#L95) · [Validators.cs#L23](AccountsService/Utils/Validators.cs#L23) | bounded optimistic-concurrency retry; PIN digit scan |
| 5 | `foreach` | [AadhaarCryptoUpgrade.cs#L39](AccountsService/Infrastructure/Data/AadhaarCryptoUpgrade.cs#L39) | batch re-encryption |
| 6 | `while` | [AadhaarCryptoUpgrade.cs#L26](AccountsService/Infrastructure/Data/AadhaarCryptoUpgrade.cs#L26) · [AdoNetAccountRepository.cs#L165](AccountsService/Infrastructure/Repositories/AdoNetAccountRepository.cs#L165) | cancellable loop; `SqlDataReader` read loop |
| 7 | Ternary `?:` and null-coalescing `??` | [InterestPolicy.cs#L30](AccountsService/Domain/Models/InterestPolicy.cs#L30) | one-line resolver |
| 8 | String interpolation | [AccountExceptions.cs#L15](AccountsService/Domain/Exceptions/AccountExceptions.cs#L15) | `$"Account {accountNumber} not found"` |
| 9 | String methods / `StringComparison` | [EncryptionManager.cs#L48](AccountsService/Utils/EncryptionManager.cs#L48) · [EncryptionManager.cs#L77](AccountsService/Utils/EncryptionManager.cs#L77) | `StartsWith`, `Substring`, ordinal comparison |
| 10 | Regular expressions | [Validators.cs#L9](UsersService/Utils/Validators.cs#L9) · [AccountDtos.cs#L13](AccountsService/DTOs/AccountDtos.cs#L13) | compiled `Regex`; `[RegularExpression]` on DTOs |
| 11 | Constants & configuration classes | [AccountRulesConfig.cs#L3](AccountsService/Domain/Models/AccountRulesConfig.cs#L3) | `const` business rules in one place |
| 12 | `static` classes / methods | [FeeCalculator.cs#L6](AccountsService/Utils/FeeCalculator.cs#L6) · [InterestPolicy.cs#L14](AccountsService/Domain/Models/InterestPolicy.cs#L14) | stateless helpers |
| 13 | `ref` parameter | [FeeCalculator.cs#L13](AccountsService/Utils/FeeCalculator.cs#L13) · call site [Account.cs#L126](AccountsService/Domain/Models/Account.cs#L126) | `Deduct(ref decimal balance, …)` mutates in place |
| 14 | `out` parameter (Try-pattern) | [EncryptionManager.cs#L87](AccountsService/Utils/EncryptionManager.cs#L87) · [AccountListCache.cs#L42](AccountsService/Utils/AccountListCache.cs#L42) | `TryDecrypt(…, out plaintext)`, `TryGet(…, out accounts)` |
| 15 | `in` parameter + `readonly struct` | [AdoNetAccountRepository.cs#L530](AccountsService/Infrastructure/Repositories/AdoNetAccountRepository.cs#L530) · use [#L153](AccountsService/Infrastructure/Repositories/AdoNetAccountRepository.cs#L153) | read-only struct passed by reference |
| 16 | `params` | [SecureConfigGuard.cs#L47](shared/Gdb.Common/Security/SecureConfigGuard.cs#L47) | `params (string Name, string? Value)[] secrets` |
| 17 | Optional / default parameters | [IAccountService.cs#L8](AccountsService/Services/IAccountService.cs#L8) · [Account.cs#L87](AccountsService/Domain/Models/Account.cs#L87) | `CancellationToken ct = default`, `DateTime? when = null` |
| 18 | Method overloading | [AdoNetAccountRepository.cs#L457](AccountsService/Infrastructure/Repositories/AdoNetAccountRepository.cs#L457) · [Money.cs#L22](AccountsService/Domain/Models/Money.cs#L22) | same name, different signatures (`Add(Money)` / `Add(decimal)`) |
| 19 | Tuples & deconstruction | [JwtUtil.cs#L35](AuthService/Security/JwtUtil.cs#L35) · [AccountController.cs#L60](AccountsService/Controllers/AccountController.cs#L60) | named tuple return; `(skip, limit) = Paging.Clamp(…)` |
| 20 | `nameof`, `Math.*` | [Money.cs#L17](AccountsService/Domain/Models/Money.cs#L17) · [PinLockout.cs#L36](AccountsService/Utils/PinLockout.cs#L36) | refactor-safe names; `Math.Ceiling` |
| 21 | Nullable reference types / `?.` | [Account.cs#L68](AccountsService/Domain/Models/Account.cs#L68) · [Account.cs#L129](AccountsService/Domain/Models/Account.cs#L129) | `AccountNumber?.Value`, `audit?.Invoke(...)` |
| 22 | `DateTime` (UTC everywhere) | [Account.cs#L70](AccountsService/Domain/Models/Account.cs#L70) | `DateTime.UtcNow` for audit-grade timestamps |
| 23 | Enums | [AccountStatus.cs#L3](AccountsService/Domain/Enums/AccountStatus.cs#L3) · [TokenStatus.cs#L3](AuthService/Domain/Models/TokenStatus.cs#L3) | state machines as enums |

## B. Object-oriented programming

| # | Concept | Where | What to look at |
|---|---|---|---|
| 24 | Class, constructor, properties, encapsulation | [Account.cs#L7](AccountsService/Domain/Models/Account.cs#L7) | `private set` properties; behaviour methods, not setters |
| 25 | Inheritance | [SavingsAccount.cs#L10](AccountsService/Domain/Models/SavingsAccount.cs#L10) · [CurrentAccount.cs#L9](AccountsService/Domain/Models/CurrentAccount.cs#L9) | `: Account` |
| 26 | Abstract class + abstract method | [Account.cs#L7](AccountsService/Domain/Models/Account.cs#L7) · [Account.cs#L98](AccountsService/Domain/Models/Account.cs#L98) | `abstract decimal GetMinimumBalance()` — every subtype must answer |
| 27 | Virtual / override (polymorphism) | [Account.cs#L105](AccountsService/Domain/Models/Account.cs#L105) · [CurrentAccount.cs#L82](AccountsService/Domain/Models/CurrentAccount.cs#L82) · [SavingsAccount.cs#L92](AccountsService/Domain/Models/SavingsAccount.cs#L92) | base default `0m`; Current overrides to ₹500 — visible on the Account Details page |
| 28 | Interfaces | [IAccountRepository.cs#L6](AccountsService/Infrastructure/Repositories/IAccountRepository.cs#L6) · [IPinLockoutService](AccountsService/Utils/PinLockout.cs#L7) | contracts with two implementations (EF / ADO.NET) |
| 29 | `sealed` | [AccountListCache.cs#L26](AccountsService/Utils/AccountListCache.cs#L26) · [AccountResponseMapper.cs#L18](AccountsService/Mapping/AccountResponseMapper.cs#L18) | closed for inheritance on purpose |
| 30 | Records (immutable DTOs, `init`) | [AccountDtos.cs#L6](AccountsService/DTOs/AccountDtos.cs#L6) | request/response contracts |
| 31 | Value object (record with behaviour) | [Money.cs#L9](AccountsService/Domain/Models/Money.cs#L9) | validation in ctor, `Add`/`Subtract` return new values |
| 32 | `with` expression (non-destructive mutation) | [UserService.cs#L235](UsersService/Services/UserService.cs#L235) | `response with { Message = … }` |
| 33 | Equality by identity (`Equals`/`GetHashCode`) | [Entity.cs#L43](shared/Gdb.Common/Domain/Entity.cs#L43) | generic entity base |
| 34 | Custom exception hierarchy | [GdbException.cs#L9](shared/Gdb.Common/Exceptions/GdbException.cs#L9) → [AccountExceptions.cs#L6](AccountsService/Domain/Exceptions/AccountExceptions.cs#L6) → [#L12](AccountsService/Domain/Exceptions/AccountExceptions.cs#L12) | base carries error code + HTTP status; subclasses specialise |
| 35 | Factory methods (static creation) | [SavingsAccount.cs#L34](AccountsService/Domain/Models/SavingsAccount.cs#L34) `Open` · [#L70](AccountsService/Domain/Models/SavingsAccount.cs#L70) `RestoreSavings` | invariants enforced at creation, private ctor |
| 36 | Extension methods | [GdbMiddlewareExtensions.cs#L14](shared/Gdb.Common/Middleware/GdbMiddlewareExtensions.cs#L14) · [Hosts.cs#L158](Gdb.Integration.Tests/Hosts.cs#L158) | `this IServiceCollection`, `this HttpResponseMessage` |
| 37 | Custom attributes | [InternalApiAttribute.cs#L18](shared/Gdb.Common/Security/InternalApiAttribute.cs#L18) | `[InternalApi]` authorization filter |

## C. Advanced C#

| # | Concept | Where | What to look at |
|---|---|---|---|
| 38 | Generics (`<T>`, constraints) | [Result.cs#L3](shared/Gdb.Common/Domain/Result.cs#L3) · [Entity.cs#L3](shared/Gdb.Common/Domain/Entity.cs#L3) · [GdbHealthExtensions.cs#L15](shared/Gdb.Common/Health/GdbHealthExtensions.cs#L15) | `Result<T>`, `Entity<TId>`, `DbContextHealthCheck<TContext> where TContext : DbContext` |
| 39 | Collections (`List`, `Dictionary`, `IReadOnlyDictionary`) | [InterestPolicy.cs#L17](AccountsService/Domain/Models/InterestPolicy.cs#L17) · [AadharVerificationService.cs#L9](AadharService/Services/AadharVerificationService.cs#L9) | case-insensitive dictionary; static lookup table |
| 40 | Concurrent collections | [RateLimitingMiddleware.cs#L11](CentralGatewayService/Middleware/RateLimitingMiddleware.cs#L11) · [Hosts.cs#L109](Gdb.Integration.Tests/Hosts.cs#L109) | `ConcurrentDictionary`, `ConcurrentQueue` |
| 41 | LINQ (query + method syntax, grouping, projection) | [AccountRepository.cs#L184](AccountsService/Infrastructure/Repositories/AccountRepository.cs#L184) · [#L199](AccountsService/Infrastructure/Repositories/AccountRepository.cs#L199) · [AccountController.cs#L66](AccountsService/Controllers/AccountController.cs#L66) | `Where/OrderBy/ToListAsync` (translated to SQL), `GroupBy/Select`, `Skip/Take` |
| 42 | Delegates: `Func`, `Action`, lambdas | [InterestPolicy.cs#L17](AccountsService/Domain/Models/InterestPolicy.cs#L17) · [Account.cs#L113](AccountsService/Domain/Models/Account.cs#L113) · [Account.cs#L138](AccountsService/Domain/Models/Account.cs#L138) | strategy map of `Func<decimal,decimal>`; `Action<string>? audit` callback |
| 43 | Higher-order functions | [AccountRepository.cs#L92](AccountsService/Infrastructure/Repositories/AccountRepository.cs#L92) | `UpdateBalanceAtomicAsync(…, Action<Account> applyDomainOperation)` |
| 44 | Iterators (`yield return`) | [TransactionDtos.cs#L60](TransactionsService/DTOs/TransactionDtos.cs#L60) | `IValidatableObject.Validate` streams results |
| 45 | Pattern matching (`is`, `is not`, property patterns) | [AccountRepository.cs#L75](AccountsService/Infrastructure/Repositories/AccountRepository.cs#L75) · [AccountController.cs#L108](AccountsService/Controllers/AccountController.cs#L108) · [RegistryResolver.cs#L43](CentralGatewayService/Resolvers/RegistryResolver.cs#L43) | `is SavingsAccount`, `is not null`, `is { Length: > 0 } url` |
| 46 | Exception handling: `try/catch/finally`, rethrow | [JwtUtil.cs#L147](AuthService/Security/JwtUtil.cs#L147) · [AuthController.cs#L53](AuthService/Controllers/AuthController.cs#L53) | `finally` disposes keys; `throw;` preserves the stack |
| 47 | Exception filters (`when`) | [AccountInternalService.cs#L128](AccountsService/Services/AccountInternalService.cs#L128) · [AdoNetAccountRepository.cs#L321](AccountsService/Infrastructure/Repositories/AdoNetAccountRepository.cs#L321) · [ExceptionHandlingMiddleware.cs#L29](shared/Gdb.Common/Middleware/ExceptionHandlingMiddleware.cs#L29) | `catch (AccountException ex) when (ex is not AccountClosedError)`; SQL error-number filter |
| 48 | `async` / `await` / `Task<T>` | [AccountService.cs#L50](AccountsService/Services/AccountService.cs#L50) | end-to-end asynchronous pipeline |
| 49 | `CancellationToken` end-to-end | [AccountController.cs#L39](AccountsService/Controllers/AccountController.cs#L39) → [IAccountService.cs#L8](AccountsService/Services/IAccountService.cs#L8) → [AccountRepository.cs#L187](AccountsService/Infrastructure/Repositories/AccountRepository.cs#L187) | request abort stops DB/HTTP work; money path deliberately uses `CancellationToken.None` after the first side effect ([TransferService.cs#L118](TransactionsService/Services/TransferService.cs#L118)) |
| 50 | `IDisposable`, `Dispose(bool)`, finalizer, `GC.SuppressFinalize` | [AccountListCache.cs#L77](AccountsService/Utils/AccountListCache.cs#L77) · [#L83](AccountsService/Utils/AccountListCache.cs#L83) · [#L98](AccountsService/Utils/AccountListCache.cs#L98) · [#L44](AccountsService/Utils/AccountListCache.cs#L44) | full dispose pattern around a `Timer`; `ObjectDisposedException.ThrowIf` |
| 51 | `using` / `await using` (deterministic cleanup) | [AdoNetTransactionRepository.cs#L31](TransactionsService/Infrastructure/Repositories/AdoNetTransactionRepository.cs#L31) · [EncryptionManager.cs#L57](AccountsService/Utils/EncryptionManager.cs#L57) | connections/commands; `AesGcm` |
| 52 | Threading primitives: `lock`, `Timer`, `PeriodicTimer`, `Channel<T>` | [RateLimitingMiddleware.cs#L72](CentralGatewayService/Middleware/RateLimitingMiddleware.cs#L72) · [AccountListCache.cs#L37](AccountsService/Utils/AccountListCache.cs#L37) · [ServiceDiscoveryHostedService.cs#L49](shared/Gdb.Common/Discovery/ServiceDiscoveryHostedService.cs#L49) · [TransactionAuditFileWriter.cs#L19](TransactionsService/Infrastructure/Audit/TransactionAuditFileWriter.cs#L19) | token bucket under lock; sweeper timer; heartbeat loop; bounded producer/consumer channel |
| 53 | File I/O | [TransactionAuditFileWriter.cs#L53](TransactionsService/Infrastructure/Audit/TransactionAuditFileWriter.cs#L53) · [NotificationStorageService.cs#L30](NotificationService/Services/NotificationStorageService.cs#L30) | append-only audit log off the request path; JSON file store |
| 54 | JSON serialization (`System.Text.Json`) | [IdempotencySettlement.cs#L33](TransactionsService/Services/IdempotencySettlement.cs#L33) · [TransactionDtos.cs#L9](TransactionsService/DTOs/TransactionDtos.cs#L9) | serialize/deserialize; `[JsonPropertyName]` snake_case contract |
| 55 | Hashing & cryptography | [EncryptionManager.cs#L50](AccountsService/Utils/EncryptionManager.cs#L50) · [EncryptionManager.cs#L102](AccountsService/Utils/EncryptionManager.cs#L102) · [AccountInternalService.cs#L181](AccountsService/Services/AccountInternalService.cs#L181) | AES-256-GCM with HKDF-derived keys; blind index (HMAC) for lookups; BCrypt for PINs |

## D. Data access

| # | Concept | Where | What to look at |
|---|---|---|---|
| 56 | EF Core `DbContext`, `DbSet`, fluent model | [AppDbContext.cs#L14](AccountsService/Infrastructure/Data/AppDbContext.cs#L14) · [#L24](AccountsService/Infrastructure/Data/AppDbContext.cs#L24) | `OnModelCreating`, indexes, unique constraints |
| 57 | Optimistic concurrency (row version) | [AppDbContext.cs#L113](AccountsService/Infrastructure/Data/AppDbContext.cs#L113) · [AccountRepository.cs#L121](AccountsService/Infrastructure/Repositories/AccountRepository.cs#L121) | `RowVersion` bumped on save; `DbUpdateConcurrencyException` → reload-apply-retry |
| 58 | EF migrations | [20260908122130_AddPerformanceIndexes.cs](AccountsService/Migrations/20260908122130_AddPerformanceIndexes.cs) | versioned schema; Production applies on startup |
| 59 | Multi-provider EF (5 databases) | [Program.cs#L61](AccountsService/Program.cs#L61) | pooled context, `EnableRetryOnFailure`, `CommandTimeout` per provider |
| 60 | ADO.NET (`SqlConnection`/`SqlCommand`/`SqlDataReader`/`SqlParameter`) | [AdoNetAccountRepository.cs#L1](AccountsService/Infrastructure/Repositories/AdoNetAccountRepository.cs#L1) · [AdoNetTransactionRepository.cs#L30](TransactionsService/Infrastructure/Repositories/AdoNetTransactionRepository.cs#L30) | parameterised SQL, `OUTPUT INSERTED.id`, reader mapping |
| 61 | Stored procedures | [AdoNetAccountRepository.cs#L175](AccountsService/Infrastructure/Repositories/AdoNetAccountRepository.cs#L175) · [AdoNetTransferLimitRepository.cs#L97](TransactionsService/Infrastructure/Repositories/AdoNetTransferLimitRepository.cs#L97) | `CommandType.StoredProcedure` (`usp_AccountSummary`, `usp_TransferDailyStats`) |
| 62 | Transactions / Unit of Work (EF + ADO.NET) | [UnitOfWork.cs#L20](TransactionsService/Services/UnitOfWork.cs#L20) · [#L44](TransactionsService/Services/UnitOfWork.cs#L44) · [#L94](TransactionsService/Services/UnitOfWork.cs#L94) | execution-strategy-aware EF transaction; ambient `TransactionScope` for ADO.NET |
| 63 | Runtime data-access toggle | [Program.cs#L143](AccountsService/Program.cs#L143) | `DATA_ACCESS=AdoNet` swaps repository implementations behind the same interface |

## E. Web API & application architecture

| # | Concept | Where | What to look at |
|---|---|---|---|
| 64 | REST controllers, routing, verbs | [AccountController.cs#L11](AccountsService/Controllers/AccountController.cs#L11) · [#L37](AccountsService/Controllers/AccountController.cs#L37) | `[Route]`, `[HttpPost]`, `Created(...)` |
| 65 | DTO validation (DataAnnotations, `IValidatableObject`) | [AccountDtos.cs#L8](AccountsService/DTOs/AccountDtos.cs#L8) · [TransactionDtos.cs#L34](TransactionsService/DTOs/TransactionDtos.cs#L34) | `[Required]`, `[Range]`, `[RegularExpression]`, cross-field rule |
| 66 | Dependency injection & lifetimes | [Program.cs#L149](AccountsService/Program.cs#L149) · [#L157](AccountsService/Program.cs#L157) · [#L158](AccountsService/Program.cs#L158) | scoped repositories, singleton services |
| 67 | Configuration binding & environment | [Program.cs#L25](AccountsService/Program.cs#L25) · [#L29](AccountsService/Program.cs#L29) | `Configuration.Bind(settings)`, env vars |
| 68 | Middleware pipeline (`IMiddleware`) | [CorrelationIdMiddleware.cs#L12](shared/Gdb.Common/Middleware/CorrelationIdMiddleware.cs#L12) · [ExceptionHandlingMiddleware.cs#L14](shared/Gdb.Common/Middleware/ExceptionHandlingMiddleware.cs#L14) · [GdbMiddlewareExtensions.cs#L29](shared/Gdb.Common/Middleware/GdbMiddlewareExtensions.cs#L29) | correlation → security headers → metrics → error boundary |
| 69 | Global error contract | [ExceptionHandlingMiddleware.cs#L29](shared/Gdb.Common/Middleware/ExceptionHandlingMiddleware.cs#L29) | `{error_code, message, detail, status, correlation_id}`; unknown errors never leak |
| 70 | JWT authentication + role authorization | [JwtUtil.cs#L35](AuthService/Security/JwtUtil.cs#L35) · [JwtValidationExtensions.cs#L28](shared/Gdb.Common/Security/JwtValidationExtensions.cs#L28) · [AccountController.cs#L38](AccountsService/Controllers/AccountController.cs#L38) | issuer/audience/alg pinned; deny-by-default `FallbackPolicy` ([#L91](shared/Gdb.Common/Security/JwtValidationExtensions.cs#L91)); `[Authorize(Roles=…)]` |
| 71 | Refresh tokens (rotation, httpOnly cookie) | [AuthService.cs#L84](AuthService/Services/AuthService.cs#L84) · [AuthController.cs#L68](AuthService/Controllers/AuthController.cs#L68) | `token_use` claim; rotation; refresh never accepted as Bearer |
| 72 | Service-to-service auth | [InternalApiAttribute.cs#L20](shared/Gdb.Common/Security/InternalApiAttribute.cs#L20) · [GatewayHandler.cs#L17](CentralGatewayService/Proxy/GatewayHandler.cs#L17) | `X-Internal-API-Key` filter; gateway strips it from external clients |
| 73 | Outbound HTTP clients (typed `HttpClient`) | [AadharClient.cs#L14](AccountsService/Integration/AadharClient.cs#L14) · [AccountServiceClient.cs#L72](TransactionsService/Integration/AccountServiceClient.cs#L72) | typed contracts, error mapping |
| 74 | Resilience: retry, circuit breaker, timeout (Polly) | [PollyPolicies.cs#L21](shared/Gdb.Common/Integration/PollyPolicies.cs#L21) | retries only for idempotent verbs; per-try timeout inside the breaker |
| 75 | Background services (`BackgroundService`) | [TransferReconciliationService.cs#L20](TransactionsService/Services/TransferReconciliationService.cs#L20) · [TransactionAuditFileWriter.cs#L15](TransactionsService/Infrastructure/Audit/TransactionAuditFileWriter.cs#L15) | reconciliation sweep; off-request-path writer |
| 76 | Caching (in-memory + distributed/Redis) | [AccountService.cs#L133](AccountsService/Services/AccountService.cs#L133) · [PinLockout.cs#L26](AccountsService/Utils/PinLockout.cs#L26) · [GdbDistributedCacheExtensions.cs#L22](shared/Gdb.Common/Caching/GdbDistributedCacheExtensions.cs#L22) | list cache with invalidation; replica-safe lockouts |
| 77 | Health checks (`/live`, `/ready`) | [GdbHealthExtensions.cs#L15](shared/Gdb.Common/Health/GdbHealthExtensions.cs#L15) · [#L58](shared/Gdb.Common/Health/GdbHealthExtensions.cs#L58) | DB-backed readiness |
| 78 | API gateway (reverse proxy, service discovery) | [GatewayHandler.cs#L22](CentralGatewayService/Proxy/GatewayHandler.cs#L22) · [RegistryResolver.cs#L12](CentralGatewayService/Resolvers/RegistryResolver.cs#L12) · [ServiceDiscoveryHostedService.cs#L26](shared/Gdb.Common/Discovery/ServiceDiscoveryHostedService.cs#L26) | header hygiene, cached resolution, register + heartbeat |
| 79 | Rate limiting (token bucket) | [RateLimitingMiddleware.cs#L6](CentralGatewayService/Middleware/RateLimitingMiddleware.cs#L6) | per-client budget, `Retry-After` |
| 80 | Pagination with hard caps | [Paging.cs](shared/Gdb.Common/Http/Paging.cs) · [AccountController.cs#L60](AccountsService/Controllers/AccountController.cs#L60) | `1 ≤ limit ≤ 1000`, `X-Total-Count` |
| 81 | OpenAPI / Swagger (gated) | [Program.cs#L135](AccountsService/Program.cs#L135) · [tools/Gdb.OpenApiGate/Program.cs](tools/Gdb.OpenApiGate/Program.cs) | docs only in Development; contract snapshots enforced in CI |

## F. Design principles & patterns

| # | Principle / pattern | Where | Why it is that pattern |
|---|---|---|---|
| 82 | **S**ingle Responsibility | [AccountService.cs#L16](AccountsService/Services/AccountService.cs#L16) vs [AccountInternalService.cs#L17](AccountsService/Services/AccountInternalService.cs#L17) | public lifecycle vs internal money movement — split from one god service |
| 83 | **O**pen/Closed | [AccountExceptions.cs#L6](AccountsService/Domain/Exceptions/AccountExceptions.cs#L6) · [AccountResponseMapper.cs#L32](AccountsService/Mapping/AccountResponseMapper.cs#L32) | a new exception declares its own status; a new account type adds a case, no switch ladders in callers |
| 84 | **L**iskov Substitution | [Account.cs#L7](AccountsService/Domain/Models/Account.cs#L7) → [SavingsAccount.cs#L10](AccountsService/Domain/Models/SavingsAccount.cs#L10) / [CurrentAccount.cs#L9](AccountsService/Domain/Models/CurrentAccount.cs#L9) | every `Account` can be credited/debited/closed identically |
| 85 | **I**nterface Segregation | [PinLockout.cs#L7](AccountsService/Utils/PinLockout.cs#L7) · [IUserServicePort.cs#L6](AuthService/Domain/Ports/IUserServicePort.cs#L6) | three-method / one-method interfaces, no fat contracts |
| 86 | **D**ependency Inversion | [AccountController.cs#L22](AccountsService/Controllers/AccountController.cs#L22) · [AuthService.cs#L23](AuthService/Services/AuthService.cs#L23) | controllers/services depend on abstractions; implementations chosen in `Program.cs` |
| 87 | Factory | [SavingsAccount.cs#L34](AccountsService/Domain/Models/SavingsAccount.cs#L34) · [CurrentAccount.cs#L33](AccountsService/Domain/Models/CurrentAccount.cs#L33) · [AccountResponseMapper.cs#L32](AccountsService/Mapping/AccountResponseMapper.cs#L32) | `Open(...)` / `Restore...(...)` static factories; type-dispatching response factory |
| 88 | Singleton | [Program.cs#L158](AccountsService/Program.cs#L158) · [AccountListCache.cs#L26](AccountsService/Utils/AccountListCache.cs#L26) | container-managed single instances (no static `Instance` anti-pattern) |
| 89 | Strategy | [InterestPolicy.cs#L17](AccountsService/Domain/Models/InterestPolicy.cs#L17) · [PollyPolicies.cs#L21](shared/Gdb.Common/Integration/PollyPolicies.cs#L21) · [Program.cs#L143](AccountsService/Program.cs#L143) | interest rate per privilege; per-request resilience policy; EF vs ADO.NET selected at startup |
| 90 | Adapter (Port & Adapter) | [IUserServicePort.cs#L6](AuthService/Domain/Ports/IUserServicePort.cs#L6) ↔ [UserServiceClient.cs#L11](AuthService/Integration/UserServiceClient.cs#L11) · [AccountServiceClient.cs#L72](TransactionsService/Integration/AccountServiceClient.cs#L72) | HTTP details adapted to a domain-shaped port; faked in tests ([Hosts.cs#L55](Gdb.Integration.Tests/Hosts.cs#L55)) |
| 91 | Bridge | [IServiceResolver.cs](CentralGatewayService/Resolvers/IServiceResolver.cs) ← [StaticResolver.cs](CentralGatewayService/Resolvers/StaticResolver.cs) / [RegistryResolver.cs#L12](CentralGatewayService/Resolvers/RegistryResolver.cs#L12) · [IAccountRepository.cs#L6](AccountsService/Infrastructure/Repositories/IAccountRepository.cs#L6) | abstraction and implementation vary independently (resolver strategy × gateway; repository × data technology) |
| 92 | Repository | [IAccountRepository.cs#L6](AccountsService/Infrastructure/Repositories/IAccountRepository.cs#L6) → [AccountRepository.cs#L11](AccountsService/Infrastructure/Repositories/AccountRepository.cs#L11) / [AdoNetAccountRepository.cs#L17](AccountsService/Infrastructure/Repositories/AdoNetAccountRepository.cs#L17) | domain talks to a collection-like contract, never to `DbContext` |
| 93 | Unit of Work | [UnitOfWork.cs#L8](TransactionsService/Services/UnitOfWork.cs#L8) · [UsersService/UnitOfWork.cs#L7](UsersService/Services/UnitOfWork.cs#L7) | one atomic boundary over several repositories |
| 94 | Observer (callbacks / events) | [CorrelationIdMiddleware.cs#L54](shared/Gdb.Common/Middleware/CorrelationIdMiddleware.cs#L54) · [JwtValidationExtensions.cs#L80](shared/Gdb.Common/Security/JwtValidationExtensions.cs#L80) · [Account.cs#L113](AccountsService/Domain/Models/Account.cs#L113) | `Response.OnStarting` subscribers; `OnTokenValidated` event hook; audit `Action<string>` observer; cross-instance invalidation by version ([CacheInvalidator.cs](TransactionsService/Utils/CacheInvalidator.cs)) |
| 95 | Saga with compensation | [TransferService.cs#L71](TransactionsService/Services/TransferService.cs#L71) · [#L209](TransactionsService/Services/TransferService.cs#L209) | PENDING → debit → credit; refund with bounded retry; `COMPENSATION_FAILED` + reconciler |
| 96 | Idempotency key | [IdempotencySettlement.cs](TransactionsService/Services/IdempotencySettlement.cs) · [DepositService.cs#L61](TransactionsService/Services/DepositService.cs#L61) | reserve → complete/replay; failure after side effect is replayed, never re-executed |
| 97 | Circuit breaker | [PollyPolicies.cs#L28](shared/Gdb.Common/Integration/PollyPolicies.cs#L28) | fail fast when a dependency is down |
| 98 | Options/Settings object | [AccountsService/Config/Settings.cs](AccountsService/Config/Settings.cs) | typed configuration bound once |

## G. Security & enterprise hardening (what a CTO will ask about)

| # | Control | Where |
|---|---|---|
| 99 | Fail-closed secrets (no dev defaults in Production, ≥32-byte keys) | [SecureConfigGuard.cs#L47](shared/Gdb.Common/Security/SecureConfigGuard.cs#L47) · [#L72](shared/Gdb.Common/Security/SecureConfigGuard.cs#L72) · [#L84](shared/Gdb.Common/Security/SecureConfigGuard.cs#L84) |
| 100 | PII encryption at rest + fail-closed decrypt + legacy upgrade | [EncryptionManager.cs#L50](AccountsService/Utils/EncryptionManager.cs#L50) · [AadhaarCryptoUpgrade.cs#L17](AccountsService/Infrastructure/Data/AadhaarCryptoUpgrade.cs#L17) · masking [AccountResponseMapper.cs#L104](AccountsService/Mapping/AccountResponseMapper.cs#L104) |
| 101 | Brute-force protection (PIN + login, replica-safe) | [FailureLockout.cs](shared/Gdb.Common/Caching/FailureLockout.cs) · [PinLockout.cs#L19](AccountsService/Utils/PinLockout.cs#L19) · [LoginThrottle.cs](AuthService/Security/LoginThrottle.cs) |
| 102 | Password policy | [Validators.cs#L29](UsersService/Utils/Validators.cs#L29) |
| 103 | Security response headers | [SecurityHeadersMiddleware.cs](shared/Gdb.Common/Middleware/SecurityHeadersMiddleware.cs) |
| 104 | Correlation ids in every log line / error body | [CorrelationIdMiddleware.cs#L61](shared/Gdb.Common/Middleware/CorrelationIdMiddleware.cs#L61) · [shared/nlog.config](shared/nlog.config) |
| 105 | Metrics (`/metrics`) + OpenTelemetry (OTLP) | [MetricsMiddleware.cs#L135](shared/Gdb.Common/Middleware/MetricsMiddleware.cs#L135) · [GdbOpenTelemetryExtensions.cs#L23](shared/Gdb.Common/Observability/GdbOpenTelemetryExtensions.cs#L23) |
| 106 | Graceful shutdown, container limits, healthchecks | [Program.cs#L208](AccountsService/Program.cs#L208) · [docker-compose.prod.yml](docker-compose.prod.yml) · [AccountsService/Dockerfile](AccountsService/Dockerfile) |
| 107 | Supply chain: NuGet audit breaks the build, central versions, warnings-as-errors | [Directory.Build.props](Directory.Build.props) · [Directory.Packages.props](Directory.Packages.props) |
| 108 | CI: build/test, dependency audit, OpenAPI drift gate, secret scan, Trivy image scan, publish | [.github/workflows/ci.yml](.github/workflows/ci.yml) |
| 109 | Production profile (fail-closed, no published ports, Redis required) | [docker-compose.prod.yml](docker-compose.prod.yml) · [docker-compose.redis.yml](docker-compose.redis.yml) · [GdbDistributedCacheExtensions.cs#L30](shared/Gdb.Common/Caching/GdbDistributedCacheExtensions.cs#L30) |

## H. Testing

| # | Concept | Where |
|---|---|---|
| 110 | Unit tests (MSTest 4), Arrange/Act/Assert | [AccountStateTests.cs](AccountsService.Tests/Domain/AccountStateTests.cs) · [MoneyTests.cs](AccountsService.Tests/Domain/MoneyTests.cs) |
| 111 | Parametrized tests (`[DataRow]`) | [AccountConceptsTests.cs#L34](AccountsService.Tests/Domain/AccountConceptsTests.cs#L34) · [PasswordPolicyTests.cs#L16](UsersService.Tests/Utils/PasswordPolicyTests.cs#L16) |
| 112 | Mocking (Moq) | [AccountInternalServiceTests.cs#L27](AccountsService.Tests/Services/AccountInternalServiceTests.cs#L27) · [TransferServiceTests.cs](TransactionsService.Tests/Services/TransferServiceTests.cs) |
| 113 | Dispose/GC behaviour under test | [AccountListCacheTests.cs#L52](AccountsService.Tests/Utils/AccountListCacheTests.cs#L52) |
| 114 | Middleware tests (`DefaultHttpContext`) | [ExceptionHandlingMiddlewareTests.cs](shared/Gdb.Common.Tests/ExceptionHandlingMiddlewareTests.cs) |
| 115 | Integration tests (real services in-process, `WebApplicationFactory`) | [Hosts.cs#L33](Gdb.Integration.Tests/Hosts.cs#L33) · [TransferSagaTests.cs](Gdb.Integration.Tests/TransferSagaTests.cs) · [AuthorizationAndErrorContractTests.cs](Gdb.Integration.Tests/AuthorizationAndErrorContractTests.cs) · [RefreshTokenTests.cs](Gdb.Integration.Tests/RefreshTokenTests.cs) |
| 116 | Contract tests (OpenAPI snapshots) | [openapi-snapshots/](openapi-snapshots/) · [tools/Gdb.OpenApiGate/SemanticDiff.cs](tools/Gdb.OpenApiGate/SemanticDiff.cs) |

---

## Verify it yourself (2 minutes)

```bash
dotnet build gdb-service-dotnet.slnx          # 0 warnings, 0 errors (warnings are errors)
dotnet test  gdb-service-dotnet.slnx          # 136 tests: 120 unit + 16 integration
dotnet run --project tools/Gdb.OpenApiGate    # 10/10 contracts match their snapshots
```

Related documents: [ENTERPRISE_ARCHITECTURE_AUDIT_2026-09-08.md](ENTERPRISE_ARCHITECTURE_AUDIT_2026-09-08.md) (audit + remediation
addendum with every commit), [SETUP_MANUAL.md](SETUP_MANUAL.md) (run it locally / Docker / production profile),
[MICROSERVICE_PATTERNS.md](MICROSERVICE_PATTERNS.md).

---

## I. EF Core (Set 4) and ADO.NET (Set 5) — full coverage map

Added after the data-access audit; every row is exercised by a test (SQLite in-memory for the relational-only paths).

| # | Concept | Where |
|---|---|---|
| 117 | `FromSqlInterpolated` (parameterised) | [AccountRepository.cs#L167](AccountsService/Infrastructure/Repositories/AccountRepository.cs#L167) — `GET /api/v1/accounts/search?q=` ([AccountController.cs#L80](AccountsService/Controllers/AccountController.cs#L80)) |
| 118 | `FromSqlRaw` (positional parameters) | [AccountRepository.cs#L176](AccountsService/Infrastructure/Repositories/AccountRepository.cs#L176) |
| 119 | Raw SQL composed with LINQ (`Where/Include/Take` over `FromSql`) | [AccountRepository.cs#L157](AccountsService/Infrastructure/Repositories/AccountRepository.cs#L157) · ADO.NET twin [AdoNetAccountRepository.cs#L173](AccountsService/Infrastructure/Repositories/AdoNetAccountRepository.cs#L173) |
| 120 | Compiled query (`EF.CompileAsyncQuery`) | [AccountRepository.cs#L141](AccountsService/Infrastructure/Repositories/AccountRepository.cs#L141) |
| 121 | Transactions + savepoints (`CreateSavepointAsync` / `RollbackToSavepointAsync`) | [AadhaarCryptoUpgrade.cs#L49](AccountsService/Infrastructure/Data/AadhaarCryptoUpgrade.cs#L49) · [#L61](AccountsService/Infrastructure/Data/AadhaarCryptoUpgrade.cs#L61) · [#L74](AccountsService/Infrastructure/Data/AadhaarCryptoUpgrade.cs#L74) |
| 122 | One-to-many relationship (Fluent API) | [Entities.cs#L37](TransactionsService/Infrastructure/Data/Entities.cs#L37) · [Entities.cs#L101](TransactionsService/Infrastructure/Data/Entities.cs#L101) · [AppDbContext.cs#L33](TransactionsService/Infrastructure/Data/AppDbContext.cs#L33) |
| 123 | Cascade vs Restrict (`OnDelete`) | Restrict [AppDbContext.cs#L36](TransactionsService/Infrastructure/Data/AppDbContext.cs#L36) · Cascade [Accounts AppDbContext.cs#L40](AccountsService/Infrastructure/Data/AppDbContext.cs#L40) |
| 124 | Eager loading + split query (`Include` + `AsSplitQuery`) | [TransactionRepository.cs#L80](TransactionsService/Infrastructure/Repositories/TransactionRepository.cs#L80) — `GET /api/v1/transactions/transfers/{id}` ([TransactionsController.cs#L138](TransactionsService/Controllers/TransactionsController.cs#L138)) |
| 125 | Explicit loading (`Entry(...).Collection(...).LoadAsync`) | [TransactionRepository.cs#L89](TransactionsService/Infrastructure/Repositories/TransactionRepository.cs#L89) · used by the reconciler [TransferReconciliationService.cs#L101](TransactionsService/Services/TransferReconciliationService.cs#L101) |
| 126 | Model seeding (`HasData`) + migration | [AppDbContext.cs#L47](TransactionsService/Infrastructure/Data/AppDbContext.cs#L47) · [AddTransferLegsAndLimitSeed](TransactionsService/Migrations/20260909062142_AddTransferLegsAndLimitSeed.cs) (`Up`/`Down`) |
| 127 | Change-tracker states (Added/Modified/Deleted/Unchanged/Detached) | [AppDbContext.cs#L78](TransactionsService/Infrastructure/Data/AppDbContext.cs#L78) · Detached→Unchanged [TransactionRepository.cs#L89](TransactionsService/Infrastructure/Repositories/TransactionRepository.cs#L89) · Deleted test [EfCoreRelationshipTests.cs#L81](TransactionsService.Tests/Infrastructure/EfCoreRelationshipTests.cs#L81) |
| 128 | `AutoDetectChangesEnabled` + `DetectChanges()` | [TransactionLogRepository.cs#L58](TransactionsService/Infrastructure/Repositories/TransactionLogRepository.cs#L58) |
| 129 | Bulk delete (`ExecuteDeleteAsync`) | [IdempotencyRepository.cs#L82](TransactionsService/Infrastructure/Repositories/IdempotencyRepository.cs#L82) · [AuthTokenRepository.cs#L56](AuthService/Infrastructure/Repositories/AuthTokenRepository.cs#L56) |
| 130 | Bulk insert: EF batched `AddRange` / ADO.NET `SqlBulkCopy` | [TransactionLogRepository.cs#L51](TransactionsService/Infrastructure/Repositories/TransactionLogRepository.cs#L51) · [AdoNetTransactionLogRepository.cs#L217](TransactionsService/Infrastructure/Repositories/AdoNetTransactionLogRepository.cs#L217) · used by seeding [Program.cs#L329](TransactionsService/Program.cs#L329) |
| 131 | SQL scalar function (`CREATE FUNCTION`, called from ADO.NET) | [ufn_DailyTransferTotal.sql](TransactionsService/Infrastructure/Data/StoredProcedures/ufn_DailyTransferTotal.sql) · [AdoNetTransactionRepository.cs#L98](TransactionsService/Infrastructure/Repositories/AdoNetTransactionRepository.cs#L98) |
| 132 | ADO.NET DELETE with affected-row count | [AdoNetAuthTokenRepository.cs#L155](AuthService/Infrastructure/Repositories/AdoNetAuthTokenRepository.cs#L155) |
| 133 | Explicit connection pooling (`Min/Max Pool Size`) | [ConnectionPooling.cs#L15](shared/Gdb.Common/Data/ConnectionPooling.cs#L15) — applied to all 8 SQL Server connection strings |
| 134 | Idempotent schema patch + object scripts run in dependency order | [schema_patch_transfer_legs.sql](TransactionsService/Infrastructure/Data/StoredProcedures/schema_patch_transfer_legs.sql) · [AdoNetSupport.cs#L49](TransactionsService/Infrastructure/Repositories/AdoNetSupport.cs#L49) |
| 135 | Migration rollback (`Down()`, `dotnet ef database update <older>`) | [AddTransferLegsAndLimitSeed.cs#L46](TransactionsService/Migrations/20260909062142_AddTransferLegsAndLimitSeed.cs#L46) · [SETUP_MANUAL §13](SETUP_MANUAL.md) |


| 136 | Inheritance mapping — TPH (`HasDiscriminator`, subtypes, `OfType`) | [Entities.cs#L16](TransactionsService/Infrastructure/Data/Entities.cs#L16) · [Entities.cs#L66](TransactionsService/Infrastructure/Data/Entities.cs#L66) · [AppDbContext.cs#L32](TransactionsService/Infrastructure/Data/AppDbContext.cs#L32) · factory [Entities.cs#L46](TransactionsService/Infrastructure/Data/Entities.cs#L46) · test [EfCoreRelationshipTests.cs#L76](TransactionsService.Tests/Infrastructure/EfCoreRelationshipTests.cs#L76) |
| 137 | Lazy loading (`ILazyLoader` injection, no proxies) | [Entities.cs#L139](TransactionsService/Infrastructure/Data/Entities.cs#L139) · [Entities.cs#L171](TransactionsService/Infrastructure/Data/Entities.cs#L171) · test [EfCoreRelationshipTests.cs#L95](TransactionsService.Tests/Infrastructure/EfCoreRelationshipTests.cs#L95) |
| 138 | Many-to-many (`HasMany().WithMany().UsingEntity`, seeded join table) — privilege tier ↔ allowed transfer modes, enforced | [AppDbContext.cs#L77](TransactionsService/Infrastructure/Data/AppDbContext.cs#L77) · [Entities.cs#L91](TransactionsService/Infrastructure/Data/Entities.cs#L91) · rule [TransferService.cs#L103](TransactionsService/Services/TransferService.cs#L103) · EF [TransferLimitRepository.cs#L28](TransactionsService/Infrastructure/Repositories/TransferLimitRepository.cs#L28) · ADO.NET join [AdoNetTransferLimitRepository.cs#L39](TransactionsService/Infrastructure/Repositories/AdoNetTransferLimitRepository.cs#L39) · migration [AddTransferModesAndTphLogs](TransactionsService/Migrations/20260909064733_AddTransferModesAndTphLogs.cs) |
| 139 | AutoMapper (profile, `ForMember`, `IncludeBase`, `AssertConfigurationIsValid`) | [UserMappingProfile.cs#L19](UsersService/Mapping/UserMappingProfile.cs#L19) · registration [Program.cs#L105](UsersService/Program.cs#L105) · use [UserService.cs#L54](UsersService/Services/UserService.cs#L54) · justified audit suppression [Directory.Build.props#L30](Directory.Build.props#L30) |

With these four, every item of EF Core Set 4 and ADO.NET Set 5 is implemented. The other services keep explicit mappers
(compile-checked); AutoMapper lives in UsersService only, under a written suppression of its advisory.

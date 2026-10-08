# ✅ .NET Bug / NFR / CR Map — trainer answer key (`dotnet-fullstack/bugs-and-crs`)

Mirror of the Python `FINAL_BUG_MAP.md`, ported to the GDB **.NET** solution. **19 bugs +
9 NFRs** are injected as code; **10 CRs** are feature-add worksheets (nothing is "broken"
for a CR — the trainee builds the missing feature).

> **Design rules honored (same as Python):**
> - **One defect per method** — no two items share a method; **no defect depends on another**.
> - **Happy path always works** — nothing injected into startup / seeding / login. `admin /
>   Welcome@1` logs in, seed accounts **#1000/#1001** exist, list → create → deposit works.
> - **Distributed across all 8 functional services** (Gateway + Registry excluded on purpose:
>   the gateway proxies everything so a bug there wouldn't be independent; registry discovery
>   is off by default so it isn't testable).
> - **.NET simplification:** EF Core uses ONE repository across all providers, so repo bugs
>   (BUG-08/13/14) are a **single** edit each (Python needed both `sqlalchemy_` + `inmemory_`).

**Tracks:** 🖥️ Browser · 🔌 API/Swagger · 🛡️ NFR (DevTools/logs/behavior) · 🧪 MSTest.

## 0) Setup
- Run the stack: `.\gdb.ps1 local inmemory` (or `docker inmemory`). App → http://localhost:3000.
- Login: `admin` / `teller` / `manager`, password **`Welcome@1`**. Seed: Savings **#1000** (GOLD, PIN 1234, Aadhaar `123456789012`), Current **#1001** (PREMIUM).
- Swagger per service: accounts `:8001/api/v1/docs`, transactions `:8002`, users `:8003`, auth `:8004`, aadhar `:8005`, company `:8006`, notification `:8007`, payment `:8008`.
- **Applying a fix:** edit file:line → restart that one service (or the stack) → hard-refresh.

---

# 🖥️ Bugs — functional

### BUG-01 — Transfer blocked exactly AT the daily limit
`TransactionsService/Utils/Validators.cs` `ValidateTransferLimit` — `currentDailyTotal + amount >= dailyLimit`.
From #1000 (GOLD daily ₹50,000) transfer so today's total lands **exactly** on ₹50,000 → rejected. **Fix:** `>=` → `>` (only *over* the limit is blocked).

### BUG-02 — Deposit drops the paise
`TransactionsService/Services/DepositService.cs` `ProcessDepositAsync` — `amount = Math.Floor(amount);` after validation.
Deposit **₹100.50** to #1000 → balance +**₹100** (the .50 lost). **Fix:** remove the `Math.Floor(amount);` line.

### BUG-03 — Transfer: recipient never receives (credit goes to sender)
`TransactionsService/Services/TransferService.cs` `ProcessTransferAsync` — `CreditAccountAsync(fromAccount, …)` (should be `toAccount`).
Transfer ₹5,000 #1000 → #1001 → "success", but **#1001 unchanged** (source debited then re-credited). **Fix:** first arg `fromAccount` → `toAccount`.

### BUG-04 — Savings allows a minor
`AccountsService/Domain/Models/AccountRulesConfig.cs` — `MIN_SAVINGS_AGE = 0`.
Create Savings with DOB age < 18 → **created**. **Fix:** `0` → `18` → rejected "must be 18+".

### BUG-05 — Current account always SILVER
`AccountsService/Services/AccountService.cs` `CreateCurrentAccountAsync` — `CurrentAccount.Open(request.Name, "SILVER", …)`.
Create Current choosing GOLD/PREMIUM → shows **SILVER**. **Fix:** `"SILVER"` → `request.Privilege`.

### BUG-06 — Close account doesn't persist
`AccountsService/Services/AccountService.cs` `CloseAccountAsync` — the `await _repository.SaveAsync(account);` line was removed.
Close an account → success toast, but it stays **Active**. **Fix:** re-add `await _repository.SaveAsync(account);` before the cache remove.

### BUG-07 — Wrong PIN → generic 500 (not clean "Invalid PIN")
`AccountsService/Services/AccountService.cs` `VerifyPinAsync` — `throw new Exception("PIN verification failed");`.
Withdraw/transfer from #1000 with a wrong PIN → generic 500 `SERVER_ERROR`. **Fix:** `Exception` → `InvalidPinError` → clean 401 "Invalid PIN".

### BUG-08 — Dashboard active-count inverted
`AccountsService/Infrastructure/Repositories/AccountRepository.cs` `GetSummaryAsync` — `CountAsync(a => !a.IsActive)`.
`GET /accounts/summary` `active_accounts` counts **inactive**. **Fix:** `!a.IsActive` → `a.IsActive`.

### BUG-09 — Company verify: unchecked status → KeyNotFound leak
`AccountsService/Integration/CompanyClient.cs` `VerifyRegistrationAsync` — the `IsSuccessStatusCode` guard was removed; it now indexes `result["status"]` on any response.
Call **accounts** `POST /api/v1/accounts/current` (Swagger :8001) with a short `registration_no` (e.g. `ABC123`) → company_crv returns 422, body has no `status` → **KeyNotFoundException** leaks. **Fix:** restore `if (response.IsSuccessStatusCode) { … } else { throw … }` before reading `status`. *(Test on accounts :8001, not company :8006 — company rejects a short CIN first.)*

### BUG-10 — Login `expires_in` is an absolute epoch, not a duration
`AuthService/Services/AuthService.cs` login dict — `{ "expires_in", ((DateTimeOffset)exp).ToUnixTimeSeconds() }`.
Swagger `:8004` login → `expires_in` is a ~1.7-billion epoch. **Fix:** → `(int)(exp - iat).TotalSeconds` (e.g. `1800`). *(No UI shows this; login works either way.)*

### BUG-11 — Wrong password logs you in
`UsersService/Domain/Models/PasswordHash.cs` `Verify` — `return true;` inside the try.
Login with a valid username + **wrong** password → logs in. **Fix:** `return true;` → `return BCrypt.Net.BCrypt.Verify(rawPassword, Hash);`.

### BUG-12 — Bad account URL shows a cached account (not "Account Not Found")
`frontend/src/store/accountStore.js` `getAccountByNumber` catch — returns `accounts[0]` on a miss.
Open an existing account (caches it), then edit the URL to `/accounts/9999` → shows an **existing** account. **Fix:** catch → `return null;`.

### BUG-13 — Duplicate-Aadhaar savings account gets created *(code + DB)*
- **13a** `AccountsService/Services/AccountService.cs` `CreateSavingsAccountAsync` — `existing.Status != AccountStatus.ACTIVE` (inverted). **Fix:** `!=` → `==`.
- **13b** `AccountsService/Infrastructure/Data/AppDbContext.cs` — the `HasIndex(s => s.AadharHash).IsUnique()` lost its `.IsUnique()`; the same unique flag was dropped in `Migrations/20260824164529_InitialCreate.cs` (+ `.Designer.cs`, `AppDbContextModelSnapshot.cs`). **Fix:** restore `.IsUnique()` in the model and `unique: true` in the migration.
Create a **savings** account with Aadhaar `123456789012` (already active on #1000) → duplicate is created. **Fix (both):** code rejects cleanly *and* the DB rejects a bypass. *(Needs a **fresh** DB to see 13b — a stale DB may still hold the old unique index.)*

### BUG-14 — Account-type filter returns the wrong type
`AccountsService/Infrastructure/Repositories/AccountRepository.cs` `GetAllAsync` — `Where(a => a.AccountType != accountType)`.
Filter Accounts by **Savings** → shows **Current**. **Fix:** `!=` → `==`.

### BUG-15 — Edited username doesn't persist
`UsersService/Infrastructure/Repositories/UserRepository.cs` `UpdateUserAsync` — `entity.Username = entity.Username;` (self-assign no-op).
Admin → edit a user's username → Save → **re-fetch (Ctrl+F5 / re-login / `GET :8003/api/v1/users/{login_id}`)** → old name returns. **Fix:** `entity.Username = updatedEntity.Username;`.

### BUG-16 — Aadhaar verify shows "NAME UNAVAILABLE"
`AadharService/Services/AadharVerificationService.cs` — `Name = "NAME UNAVAILABLE"`.
Create Savings → verify Aadhaar `123456789012` → verifies green but **Name = "NAME UNAVAILABLE"**. **Fix:** → `holderData.Name`.

### BUG-17 — Company verify shows type "UNKNOWN"
`CompanyCrvService/Services/CompanyVerificationService.cs` — `Type = "UNKNOWN"`.
Create Current → verify a valid CIN → **Type = "UNKNOWN"**. **Fix:** → `companyData.Type`.

---

# 🔌 API/Swagger only
BUG-09 (above) and BUG-10 (above) have no dedicated screen — test them on Swagger.

---

# 🛡️ NFRs

### NFR-01 — PIN written to logs in plaintext *(Security · Critical)*
`AccountsService/Services/AccountInternalService.cs` `VerifyPinInternalAsync` — `_logger.LogInformation("Verifying PIN {Pin} …", pin, …)`.
Do any PIN action → the accounts log prints the raw PIN. **Fix:** remove `{Pin}`/`pin` from the log.

### NFR-02 — Aadhaar not masked in the response *(Security · High)*
`AccountsService/DTOs/AccountDtos.cs` — mask keeps `Substring(0)` (all 12 shown).
`GET /accounts/1000` Response → `"aadhar_number":"********123456789012"`. **Fix:** `Substring(0)` → `Substring(8)`.

### NFR-03 — Auth bypass: read a user with NO token *(Security · Critical)*
`UsersService/Controllers/UsersController.cs` `ViewUser` — `[AllowAnonymous]`.
`curl -i http://localhost:8003/api/v1/users/admin` (no token) → **200**. **Fix:** `[AllowAnonymous]` → `[Authorize(Roles = "MANAGER,TELLER,ADMIN")]`.

### NFR-04 — accounts→aadhar client has no timeout *(Reliability)*
`AccountsService/Program.cs` — `IAadharClient` `client.Timeout = Timeout.InfiniteTimeSpan`.
A hung aadhar service makes account **submit** hang forever. **Fix:** → `TimeSpan.FromSeconds(5)`.

### NFR-05 — transactions→accounts validate call has no timeout *(Reliability)*
`TransactionsService/Program.cs` — `AccountServiceClient` client timeout set to `InfiniteTimeSpan`, and the sibling calls (`VerifyPin`/`Debit`/`Credit`) carry a per-call 10s token while `ValidateAccountAsync` does not.
A hung accounts service makes deposit/withdraw/transfer hang forever (validate step). **Fix:** restore `c.Timeout = TimeSpan.FromSeconds(10)` on the client in Program.cs (and/or drop the per-sibling tokens).

### NFR-06 — Edit-user not audited *(Observability)*
`UsersService/Services/UserService.cs` `EditUserAsync` — the `AuditService.LogActionAsync(…, "UPDATE", …)` call was removed (add/activate/inactivate still audit).
Edit a user → no `UPDATE` row in the audit log. **Fix:** re-add the audit call after the update.

### NFR-07 — List endpoint ignores page size *(Performance)*
`AccountsService/Controllers/AccountController.cs` `GetAllAccounts` — the `if (limit.HasValue) query = query.Take(limit.Value);` line was removed.
`GET /accounts?skip=0&limit=5` returns **all** rows. **Fix:** re-add the `.Take(limit.Value)`.

### NFR-08 — Payment error leaks the raw internal message *(Security · High)*
`CentralPaymentGatewayService/Controllers/PaymentController.cs` `ProcessPayment` — a leaky `catch (Exception e) { return StatusCode(500, new { detail = e.Message }); }`.
Trigger a failure in `POST :8008/api/v1/payment/process` → body leaks the raw exception text. **Fix:** remove the leaky catch (let the middleware return a generic error) or return a generic message.

### NFR-09 — Internal API key not enforced *(Security · High)*
`NotificationService/Controllers/NotificationController.cs` — the class-level `[InternalApi]` was removed.
`POST :8007/api/v1/notify/...` with **no** `X-Internal-API-Key` → succeeds. **Fix:** re-add `[InternalApi]` on the controller.

---

# 🧪 MSTest (red → green)
`AccountsService.Tests/Domain/MoneyTests.cs` — run `dotnet test AccountsService.Tests --filter MoneyTests`.
- **BUG-18** `Add_SumsAmounts` asserts `160m` (actual 150). **Fix:** `160m` → `150m`.
- **BUG-19** `Subtract_WithinBalance_Reduces` asserts `70m` (actual 60). **Fix:** `70m` → `60m`.

---

# 🔧 CRs — feature-ADD worksheets (10) — nothing is broken; build the feature

| CR | Build (service layer) | Where (.NET) |
|---|---|---|
| **CR-01** min-balance on savings withdrawal | reject a SAVINGS withdrawal leaving balance < ₹500 | `TransactionsService/Services/WithdrawService.cs` (`ProcessWithdrawAsync`, extend `Utils/Validators.ValidateBalance`) |
| **CR-02** RTGS fee | mode=RTGS & amount ≥ ₹2,00,000 → deduct ₹25 | `TransactionsService/Services/TransferService.cs` (`ProcessTransferAsync`; `TransferMode.RTGS` exists) |
| **CR-03** PLATINUM tier | add PLATINUM privilege + higher limits | `TransactionsService/Services/TransferLimitService.cs` + seed in `Program.cs` (PREMIUM/GOLD/SILVER today) |
| **CR-04** daily deposit cap | reject deposits pushing the day's total > ₹5,00,000 | `TransactionsService/Services/DepositService.cs` (use a daily sum like `GetDailyTransferTotalAsync`) |
| **CR-05** freeze / unfreeze | reversible frozen state that blocks debits | `AccountsService/Services/AccountService.cs` (new freeze/unfreeze) + a UI action |
| **CR-06** savings interest | show accrued simple interest (P×R×T/100) on savings details | `AccountsService/Services/AccountService.cs` `GetAccountDetails` |
| **CR-07** date-range statement | filter statement to inclusive from/to | `TransactionsService/Services/TransactionLogService.cs` (start/end already flow to the repo; expose/validate at the controller) |
| **CR-08** sort accounts | return accounts sorted (balance desc / name) | `AccountsService` list/summary path |
| **CR-09** settings persistence | persist a preference and reload it | `UsersService` settings service |
| **CR-10** self-transfer SELF tag | detect same-holder transfer, tag SELF | `TransactionsService/Services/TransferService.cs` (currently self-transfer is rejected — convert to an allowed tagged path) |

CRs are open-ended enhancements — they are **not** pre-built in this branch (matching the Python design).

---

## Distribution (independence + spread)
| Service | Items |
|---|---|
| Accounts | BUG-04,05,06,07,08,09,13,14 · NFR-01,02,04,07 |
| Transactions | BUG-01,02,03 · NFR-05 |
| Users | BUG-11,15 · NFR-03,06 |
| Auth | BUG-10 |
| Aadhar | BUG-16 |
| Company | BUG-17 |
| Notification | NFR-09 |
| Payment Gateway | NFR-08 |
| Frontend | BUG-12 |
| Tests | BUG-18,19 |

Every defect is in its **own method** — fixing one never touches another, and none depends on another being fixed first.

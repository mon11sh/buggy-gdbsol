# Backward-Compatibility Contracts — Phase 0

Everything here is a **frozen contract**: it must not change accidentally during the enterprise migration. Any change requires an explicit compatibility decision + a passing regression check. Grounded in actual code.

---

## 1. Request payload fields (per key DTO)
**accounts** (`accounts_service/app/models/account.py`, C# DTOs BaseModel):
- `SavingsAccountCreate` (L39): `name:str(2-255)`, `privilege:Literal[PREMIUM,GOLD,SILVER]=SILVER`, `bank_name:str="Global Digital Bank"`, `bank_branch:str="Main Branch"`, `ifsc_code:str="GDB0000001"`, `pin:str(4)`, `date_of_birth:str(YYYY-MM-DD)`, `gender:Literal[Male,Female,Others]`, `phone_no:str(10)`, `aadhar_number:str(12)`, `account_type:Literal[SAVINGS]="SAVINGS"`, `initial_balance:float>=2000.0 (default 2000.0)`.
- `CurrentAccountCreate` (L79): base fields + `pin:str(4)`, `company_name:str(1-255)`, `registration_no:str(1-50)`, `account_type:Literal[CURRENT]="CURRENT"`, `website:Optional[str](<=255)`.
- `DebitRequest`/`CreditRequest` (L133/142): `account_number:int`, `amount:float>0`, `description:Optional[str]`, `idempotency_key:Optional[str]`.
- `AccountUpdate` (L167): all optional — `name`, `privilege`, `phone_no`, `company_name`, `website`.

**transactions** (`app/models/transaction.py`, @dataclass):
- `FundTransferCreate` (L15): `from_account:int>0`, `to_account:int>0`, `transfer_amount:Decimal>0 (2dp)`, `transfer_mode:Literal[NEFT,RTGS,IMPS,UPI,CHEQUE]`. ⚠️ DTO allows **CHEQUE** but DB CHECK (postgres) only allows NEFT/RTGS/IMPS/UPI.
- `TransactionLoggingCreate` (L45): `amount:Decimal>0 (2dp)`, `transaction_type:Literal[WITHDRAW,DEPOSIT,TRANSFER]`.
- Note: the live `POST /deposits|/withdrawals|/transfers` handlers take **primitive query params** (account_number, amount, pin, description) + `Idempotency-Key` header — not these DTOs. The DTOs back the `frontend_routes` `/api/v1/transactions/*` endpoints.

**auth** (`app/models/auth_models.py`): `LoginRequest` (L14): `login_id:str(1-255)`, `password:str(1-1000)`.

**users** (`app/models/request_models.py`, C# DTOs):
- `AddUserRequest` (L11): `username:str(1-255)`, `login_id:str(3-50, regex ^[a-zA-Z0-9._-]+$)`, `password:str(>=8)`, `role:Optional[str]` (MANAGER/TELLER/ADMIN, default MANAGER).
- `EditUserRequest` (L49): optional `username`, `password(>=8)`, `role`.

## 2. Response payload fields
**accounts** (`account.py`): `AccountResponse` (L89): base + `account_number:int`, `account_type:Literal[SAVINGS,CURRENT]`, `balance:float`, `is_active:bool`, `activated_date:datetime`, `closed_date:Optional[datetime]`. `SavingsAccountResponse` (L100) adds `date_of_birth:str`, `gender:str`, `phone_no:str`, `aadhar_number:str` **masked `********NNNN`** (L108-114). `CurrentAccountResponse` (L117) adds `company_name`, `registration_no`, `website`. `BalanceResponse` (L125): `account_number:int`, `balance:float`, `currency:str="INR"`.
**transactions** (`transaction.py`): `FundTransferResponse` (L37) = create fields + `id:int`, `created_at`, `updated_at`. `TransactionLoggingResponse` (L65) = create fields + `id`, `created_at`, `updated_at`. (Note: routes declare `response_model=dict`, so the wire shape is the handler's dict, not necessarily these DTOs.)
**auth**: `TokenResponse` (L21): `access_token:str`, `token_type:str="Bearer"`, `expires_in:int`, `user_id:int`, `login_id:str`, `role:str`.

## 3. HTTP status codes (must not change)
- **auth** `POST /api/v1/auth/login` → 200; 404 user_not_found, 401 inactive/invalid, 429 throttled, 503 upstream, 500.
- **accounts** create savings/current → **201**; GET endpoints → 200; PUT/activate/inactivate/close → 200; verify-pin → 200 with 423 (lockout), 401, 404.
- **accounts internal** debit/credit → 200 (400 on amount≤0, 423 lockout, 500).
- **transactions** deposits/withdrawals/transfers (both `/api/v1/*` and `/api/v1/transactions/*`) → **201**; GET logs/limits → 200; log fetch failure → 503.
- **users** add → **201** (409 duplicate); edit/list/view → 200 (404/403); activate/inactivate → 200.

## 4. Error response formats
- **transactions** global handlers (`main.py:260-288`): `TransactionException` → `{"error_code","message","status":"error"}` at `exc.http_code`; unhandled → 500 `{"error_code":"INTERNAL_ERROR","message":"Internal server error","status":"error"}`.
- **auth** (`main.py:77-86`): `{"error":<ClassName>,"message"}` at `exc.status_code`.
- **accounts**: `ErrorResponse` dataclass (`account.py:187`) → `{"error_code","message","timestamp","path?"}`.
- **users**: route-level `HTTPException(detail=...)` (string or dict); generic 500 → `detail="Internal server error"`.
- **shared internal-auth** (`gdb_common/internal_auth.py:37-43`): 401 `detail={"error_code":"UNAUTHORIZED","message":"Missing or invalid internal API key"}`.

## 5. Route paths + API versions
- All user-facing routes under **`/api/v1`**. Full route list: see `api-route-inventory.md`.
- Docs: `/api/v1/docs`, `/api/v1/redoc`, `/api/v1/openapi.json` (accounts/users/transactions). ⚠️ **auth is inconsistent** — docs at `/api/v1/docs` but `openapi_url="/openapi.json"` and redoc at `/redoc` (`auth_service/app/main.py:59-61`). Preserve as-is.
- accounts internal router prefix `/api/v1/internal`; **users internal router prefix `/internal/v1`** (different — preserve both).
- transactions `frontend_routes` group prefix `/api/v1/transactions` (`/deposit`, `/withdraw`, `/transfer`, `/account/{account_number}`).

## 6. Authentication headers
- **`Authorization: Bearer <JWT>`** — OpenAPI security scheme `Bearer` (http/bearer/JWT) in accounts/transactions/users `custom_openapi()`. Tokens issued by auth `/api/v1/auth/login`. Verified via `gdb_common.auth_dependencies` (HS256 default; RS256 when `JWT_PUBLIC_KEY` set).

## 7. Internal service headers (exact names)
- **`X-Internal-API-Key`** — `Header(None, alias="X-Internal-API-Key")` (`gdb_common/internal_auth.py:22`), constant-time compare vs `INTERNAL_API_KEY`.
- **`X-Correlation-ID`** — read/minted + echoed by observability middleware (`gdb_common/observability.py:96-99`).
- **`Idempotency-Key`** — transactions `IDEMPOTENCY_HEADER_NAME` (`settings.py:139`).

## 8. Provider names (the 5 `DATABASE_PROVIDER` values)
`sqlite`, `mysql`, `postgres`, `supabase`, `inmemory`. Default `postgres`. Routing: accounts/users/auth → Entity Framework Core for the 4 SQL, InMemory for inmemory; transactions → Asyncpg for postgres/supabase, Entity Framework Core for sqlite/mysql, InMemory for inmemory.

## 9. Database table & column names, indexes, unique, FK
See `database-schema-baseline.md`. Frozen: table names (`accounts`, `savings_account_details`, `current_account_details`, `fund_transfers`, `transaction_logging`, `transfer_limits`, `idempotency_keys`, `users`, `user_audit_logs`, `auth_tokens`, `auth_audit_logs`); the unique constraints, natural PKs (`transfer_limits.privilege`, `idempotency_keys.idempotency_key`), and the two CASCADE FKs on account details.

## 10. Service ports (`run_all.py:38-50`)
| Service | Port | | Service | Port |
|---|---|---|---|---|
| central_gateway | 8000 | | company_crv | 8006 |
| accounts | 8001 | | notification | 8007 |
| transactions | 8002 | | central_payment_gateway | 8008 |
| users | 8003 | | registry | 8010 |
| auth | 8004 | | *(frontend)* | 3000 |
| aadhar | 8005 | | | |

## 11. Inter-service URLs (settings defaults, identical across services)
`ACCOUNTS_SERVICE_URL`/`ACCOUNT_SERVICE_URL`=`http://localhost:8001`; `TRANSACTIONS_SERVICE_URL`/`TRANSACTION_SERVICE_URL`=`:8002`; `USERS_SERVICE_URL`/`USER_SERVICE_URL`=`:8003`; `AUTH_SERVICE_URL`=`:8004`; `AADHAR_SERVICE_URL`=`:8005`; `COMPANY_SERVICE_URL`=`:8006`; `NOTIFICATION_SERVICE_URL`=`:8007`; `PAYMENT_GATEWAY_SERVICE_URL`=`:8008`. **Both plural and legacy-singular aliases exist** for accounts/transactions/users — preserve both.

---

## Highest-risk compatibility traps (verify before/after every change)
1. **create_all is authoritative** — renaming an ORM column/table silently changes the live schema on next boot (no migration guard). 
2. **transactions provider divergence** — CHECK constraints exist on postgres only; a change that relies on DB-level enforcement behaves differently on sqlite/mysql.
3. **Aadhaar masking + encryption** — `********NNNN` response shape and the `aadhar_hash` blind index are contracts.
4. **Dual env-var aliases** (plural/singular service URLs; `DATABASE_*` vs `DB_*`) — dropping one breaks configured deployments.
5. **auth OpenAPI path inconsistency** and **users `/internal/v1` prefix** — clients may depend on these exact paths.



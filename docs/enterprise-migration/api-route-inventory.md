# API Route Inventory — Phase 0 Baseline

**Scope:** `gdb-service` (solution). Every public and internal route across all 10 services, grounded in the actual `@router.*` / `@app.*` decorators.
**Do not change any of these paths, methods, status codes, models, or auth requirements without a compatibility review** (see `backward-compatibility-contracts.md`).

**Conventions**
- *Full path* includes the app-level `include_router(prefix=...)` **and** any prefix declared on the `APIRouter`.
- Factory-style auth deps are called in the signature (`require_admin()`, `require_admin_or_teller()`, `require_admin_or_teller_or_manager()`); object-style deps are passed by reference (`get_current_user`, `require_manager_or_teller_dependency`, `require_admin_or_teller_dependency`).
- Internal API key is enforced at `include_router(dependencies=[Depends(verify_internal_api_key)])` level (whole router), never per-route.
- ASP.NET Core Web API default success = 200 unless `status_code=` set.

---

## accounts_service (port 8001)

Router mounting (`accounts_service/app/main.py`):
- `account_routes.router` → `include_router(prefix=settings.api_prefix)`, `API_PREFIX="/api/v1"` (L133-137). Auth per-route.
- `internal_accounts.router` → `include_router(prefix="/api/v1/internal", dependencies=[Depends(verify_internal_api_key)])` (L139-144).
- App-level: `GET /health` (L147), `GET /` (L163), `GET /live` (L189), `GET /ready` (L195).

### Public — `app/api/account_routes.py`
| Method | Full path | Handler (line) | Request model | Response model | Success | Auth | Roles | Internal key | Exceptions |
|---|---|---|---|---|---|---|---|---|---|
| POST | /api/v1/accounts/savings | create_savings_account (76) | SavingsAccountCreate | AccountResponse | 201 | require_admin_or_teller() | ADMIN, TELLER | no | AccountException→400, Exception→500 |
| POST | /api/v1/accounts/current | create_current_account (159) | CurrentAccountCreate | AccountResponse | 201 | require_admin_or_teller() | ADMIN, TELLER | no | AccountException→400, Exception→500 |
| GET | /api/v1/accounts | get_all_accounts (244) | query: account_type, skip, limit | list[AccountResponse] | 200 | require_admin_or_teller_or_manager() | ADMIN, TELLER, MANAGER | no | invalid type→400, Exception→500 |
| GET | /api/v1/accounts/summary | get_accounts_summary (329) | none | dict | 200 | require_admin_or_teller_or_manager() | ADMIN, TELLER, MANAGER | no | AccountException→400, Exception→500 |
| GET | /api/v1/accounts/{account_number} | get_account (372) | path int | dict | 200 | require_admin_or_teller_or_manager() | ADMIN, TELLER, MANAGER | no | AccountException→404/400, Exception→500 |
| GET | /api/v1/accounts/{account_number}/balance | get_balance (436) | path int | BalanceResponse | 200 | require_admin_or_teller_or_manager() | ADMIN, TELLER, MANAGER | no | AccountException→404/400, Exception→500 |
| PUT | /api/v1/accounts/{account_number} | update_account (518) | AccountUpdate | dict | 200 | require_admin_or_teller() | ADMIN, TELLER | no | AccountException→404/400, Exception→500 |
| POST | /api/v1/accounts/{account_number}/activate | activate_account (581) | none | dict | 200 | require_admin() | ADMIN | no | AccountException→404/409/400, Exception→500 |
| POST | /api/v1/accounts/{account_number}/inactivate | inactivate_account (644) | none | dict | 200 | require_admin() | ADMIN | no | AccountException→404/409/400, Exception→500 |
| POST | /api/v1/accounts/{account_number}/close | close_account (707) | none | dict | 200 | require_admin() | ADMIN | no | AccountException→404/400, Exception→500 |
| POST | /api/v1/accounts/{account_number}/verify-pin | verify_pin (776) | PinVerifyRequest | dict | 200 | get_current_user | any authenticated | no | lockout→423 (Retry-After), invalid→401, AccountException→404/401, Exception→500 |

### Internal — `app/api/internal_accounts.py` (whole router behind `verify_internal_api_key`)
| Method | Full path | Handler (line) | Request | Success | Exceptions |
|---|---|---|---|---|---|
| GET | /api/v1/internal/accounts/{account_number} | get_account_details_internal (31) | path int | 200 | AccountException→404/500 |
| GET | /api/v1/internal/accounts/{account_number}/privilege | get_privilege_internal (62) | path | 200 | Exception→500 |
| GET | /api/v1/internal/accounts/{account_number}/active | check_account_active_internal (85) | path | 200 | Exception→500 |
| POST | /api/v1/internal/accounts/{account_number}/debit | debit_account_internal (112) | path int; query amount, description | 200 | amount≤0→400, Exception→500 |
| POST | /api/v1/internal/accounts/{account_number}/credit | credit_account_internal (154) | path int; query amount, description | 200 | amount≤0→400, Exception→500 |
| POST | /api/v1/internal/accounts/{account_number}/verify-pin | verify_pin_internal (200) | path int; query pin | 200 | lockout→423, invalid→400, Exception→500 |

App-level: `GET /health`, `GET /`, `GET /live`, `GET /ready` — unauthenticated.

---

## users_service (port 8003)

Router mounting (`users_service/app/main.py` L125-130): public routers each declare `prefix="/api/v1"` (included with no extra prefix). `internal_user_router` declares `prefix="/internal/v1"`, included with `dependencies=[Depends(verify_internal_api_key)]`. App-level: `GET /api/v1/health` (133), `GET /` (146), `GET /live` (168), `GET /ready` (174).

### Public
| Method | Full path | Handler (file:line) | Request | Response | Success | Auth | Roles | Exceptions |
|---|---|---|---|---|---|---|---|---|
| POST | /api/v1/users | add_user (add_user_routes.py:41) | AddUserRequest | AddUserResponse | 201 | require_admin() | ADMIN | UserAlreadyExists→409, InvalidInput→400, Exception→500 |
| PUT | /api/v1/users/{login_id} | edit_user (edit_user_routes.py:42) | EditUserRequest | EditUserResponse | 200 | require_admin() | ADMIN | UserNotFound→404, UserInactive→403, Exception→500 |
| GET | /api/v1/users/{login_id} | view_user (view_user_routes.py:50) | path | ViewUserResponse | 200 | get_current_user | self OR ADMIN (in-handler 403) | UserNotFound→404, Exception→500 |
| GET | /api/v1/users | list_users (view_user_routes.py:127) | none | ListUsersResponse | 200 | require_admin_or_teller() | ADMIN, TELLER | Exception→500 |
| PATCH | /api/v1/users/{login_id}/inactivate | inactivate_user (inactivate_user_routes.py:39) | path | InactivateUserResponse | 200 | require_admin() | ADMIN | UserNotFound→404, AlreadyInactive→400, Exception→500 |
| PATCH | /api/v1/users/{login_id}/activate | activate_user (activate_user_routes.py:40) | path | InactivateUserResponse | 200 | require_admin() | ADMIN | UserNotFound→404, AlreadyActive→400, Exception→500 |

### Internal — `app/api/internal_user_routes.py` (prefix `/internal/v1`, behind `verify_internal_api_key`)
| Method | Full path | Handler (line) | Request | Response | Success | Exceptions |
|---|---|---|---|---|---|---|
| POST | /internal/v1/users/verify | verify_user_credentials (80) | Body login_id, password | VerifyCredentialsResponse | 200 | Exception→500 |
| GET | /internal/v1/users/{login_id}/status | get_user_status (144) | path | dict | 200 | None→404, Exception→500 |
| GET | /internal/v1/users/{login_id}/role | get_user_role (198) | path | dict | 200 | None→404, Exception→500 |
| POST | /internal/v1/users/validate-role | validate_user_role (255) | Body login_id, required_role | dict | 200 | None→404, Exception→500 |
| POST | /internal/v1/users/bulk-validate | bulk_validate_users (314) | BulkValidateRequest | dict | 200 | empty→400, Exception→500 |
| GET | /internal/v1/health | health_check (382) | none | dict | 200 | none |

App-level: `GET /api/v1/health`, `GET /`, `GET /live`, `GET /ready` — unauthenticated.

---

## transactions_service (port 8002)

Router mounting (`transactions_service/app/main.py` L237-242): six routers, each declares its own prefix (`/api/v1`; frontend_routes uses `/api/v1/transactions`). No router-level auth/internal key — auth per-route. Global handlers: `TransactionException`→`exc.http_code` (260), `Exception`→500 (276). App-level: `GET /api/v1/health` (168), `GET /` (188), `GET /ready` (217), `GET /live` (302).

| Method | Full path | Handler (file:line) | Request | Success | Auth | Roles | Exceptions |
|---|---|---|---|---|---|---|---|
| POST | /api/v1/deposits | deposit_funds (deposit_routes.py:41) | query account_number, amount, description; Header Idempotency-Key | 201 | require_manager_or_teller_dependency | MANAGER, TELLER | TransactionException→http_code, Exception→500 |
| POST | /api/v1/withdrawals | withdraw_funds (withdraw_routes.py:42) | query account_number, amount, pin, description; Header Idempotency-Key | 201 | require_manager_or_teller_dependency | MANAGER, TELLER | TransactionException→http_code, Exception→500 |
| POST | /api/v1/transfers | transfer_funds (transfer_routes.py:44) | query from_account, to_account, amount, pin, transfer_mode, description; Header Idempotency-Key | 201 | require_manager_or_teller_dependency | MANAGER, TELLER | TransactionException→http_code, Exception→500 |
| GET | /api/v1/transfer-limits/{account_number} | get_transfer_limit (transfer_limit_routes.py:47) | path | 200 | get_current_user | any auth | TransactionException→http_code, Exception→500 |
| GET | /api/v1/transfer-limits/remaining/{account_number} | get_remaining_limit (transfer_limit_routes.py:73) | path | 200 | get_current_user | any auth | TransactionException→http_code, Exception→500 |
| GET | /api/v1/transfer-limits/rules/all | get_all_transfer_rules (transfer_limit_routes.py:99) | none | 200 | require_admin_or_teller_or_manager() | ADMIN, TELLER, MANAGER | Exception→500 |
| POST | /api/v1/transfer-limits/check | check_can_transfer (transfer_limit_routes.py:120) | query account_number, amount | 200 | get_current_user | any auth | TransactionException→http_code, Exception→500 |
| PUT | /api/v1/transfer-limits/rules/{privilege} | update_transfer_rule (transfer_limit_routes.py:150) | TransferLimitRuleUpdate | 200 | require_admin_or_teller_dependency | ADMIN, TELLER | ValueError→400, Exception→500 |
| GET | /api/v1/transaction-logs | get_all_transactions (transaction_log_routes.py:34) | query skip, limit, type, start_date, end_date, sort_by, order | 200 | require_admin_or_teller_or_manager() | ADMIN, TELLER, MANAGER | TransactionException→http_code, Exception→500 |
| GET | /api/v1/transaction-logs/{account_number} | get_transaction_logs (transaction_log_routes.py:72) | path + query | 200 | get_current_user | any auth | ValueError→400, Exception→503 |
| GET | /api/v1/transaction-logs/summary/{account_number} | get_transaction_summary (transaction_log_routes.py:119) | path + query | 200 | get_current_user | any auth | ValueError→400, Exception→500 |
| POST | /api/v1/transactions/deposit | deposit (frontend_routes.py:95) | DepositRequest | 201 | require_manager_or_teller_dependency | MANAGER, TELLER | TransactionException→http_code, Exception→500 |
| POST | /api/v1/transactions/withdraw | withdraw (frontend_routes.py:142) | WithdrawRequest | 201 | require_manager_or_teller_dependency | MANAGER, TELLER | TransactionException→http_code, Exception→500 |
| POST | /api/v1/transactions/transfer | transfer (frontend_routes.py:190) | TransferRequest | 201 | require_manager_or_teller_dependency | MANAGER, TELLER | TransactionException→http_code, Exception→500 |
| GET | /api/v1/transactions | get_all_transactions (frontend_routes.py:257) | query | 200 | require_admin_or_teller_or_manager() | ADMIN, TELLER, MANAGER | ValueError→400, Exception→500 |
| GET | /api/v1/transactions/account/{account_number} | get_transactions_by_account (frontend_routes.py:304) | path + query | 200 | get_current_user | any auth | ValueError→400, Exception→500 |

All transaction responses are `response_model=dict` (weakly typed). App-level: `GET /api/v1/health`, `GET /`, `GET /ready`, `GET /live` — unauthenticated.

---

## auth_service (port 8004)

Router mounting (`auth_service/app/main.py` L90): `include_router(auth_router)`; router declares `prefix="/api/v1/auth"`. Global handler `AuthenticationException`→`exc.status_code` (77). App-level: `GET /` (94), `GET /health` (105), `GET /ready` (115, returns 503 on DB failure), `GET /live` (147).

### `app/api/auth_routes.py`
| Method | Full path | Handler (line) | Request | Response | Success | Auth | Exceptions |
|---|---|---|---|---|---|---|---|
| POST | /api/v1/auth/login | login (46) | LoginRequest (+ Request) | TokenResponse | 200 | public | throttle→429, UserNotFound→404, UserInactive→401, InvalidCredentials→401, ServiceUnavailable→503, Exception→500 |
| GET | /api/v1/auth/health | health_check (117) | none | dict | 200 | public | none |
| GET | /api/v1/auth/verify | verify_token (133) | Authorization header | dict | 200 | manual Bearer parse | missing/invalid→401 |
| POST | /api/v1/auth/logout | logout (170) | Authorization header | dict | 200 | public | Exception swallowed→success |
| POST | /api/v1/auth/register | register (199) | none | dict | 201 declared | public | always 403 (registration_not_allowed) |

App-level unauthenticated: `GET /`, `GET /health`, `GET /ready`, `GET /live`.

---

## notification_service (port 8007)

Router mounting (`notification_service/app/main.py` L34): `include_router(notify_router, prefix="/api/v1/notify", dependencies=[Depends(verify_internal_api_key)])`. App-level: `GET /health` (36), `GET /live` (57), `GET /ready` (63).

| Method | Full path | Handler (line) | Request | Success | Internal key | Exceptions |
|---|---|---|---|---|---|---|
| POST | /api/v1/notify/send | send_notification (19) | NotificationRequest | 200 | yes (router) | Exception→500 |
| GET | /api/v1/notify/{identifier} | get_notifications (32) | path | 200 | yes | global |
| POST | /api/v1/notify/read/{identifier} | mark_as_read (38) | path | 200 | yes | global |
| DELETE | /api/v1/notify/{identifier} | clear_notifications (45) | path | 200 | yes | global |

App-level unauthenticated: `GET /health`, `GET /live`, `GET /ready`.

---

## central_payment_gateway_service (port 8008)

Router mounting (`.../app/main.py` L30-35): `include_router(payment_router, prefix="/api/v1/payment", dependencies=[Depends(verify_internal_api_key)])`. App-level: `GET /health` (37), `GET /live` (57), `GET /ready` (63).

| Method | Full path | Handler (line) | Request | Response | Success | Internal key | Exceptions |
|---|---|---|---|---|---|---|---|
| POST | /api/v1/payment/process | process_payment (routes.py:20) | PaymentRequest | PaymentResponse | 200 | yes | Exception→500 |
| POST | /api/v1/payment/validate | validate_payment (routes.py:36) | ValidationRequest | ValidationResponse | 200 | yes | Exception→500 |

App-level unauthenticated: `GET /health`, `GET /live`, `GET /ready`.

---

## aadhar_service (port 8005)

Routes declared directly on `app` in `aadhar_service/app/main.py`. `API_V1_PREFIX="/api/v1"`. **`verify_internal_api_key` is imported (L19) but NOT applied — these endpoints are unauthenticated.** Global `Exception` handler at L138. Startup/shutdown use deprecated `@app.on_event`.

| Method | Full path | Handler (line) | Request | Response | Success | Auth | Exceptions |
|---|---|---|---|---|---|---|---|
| GET | /health | health_check (60) | none | dict | 200 | none | global |
| GET | /api/v1/verify/{aadhar_number} | verify_aadhar_get (73) | path | AadharVerificationResponse | 200 | none | ValueError→400, Exception→500 |
| POST | /api/v1/verify | verify_aadhar (101) | AadharVerificationRequest | AadharVerificationResponse | 200 | none | ValueError→400, Exception→500 |
| GET | /api/v1/valid-numbers | get_valid_numbers (123) | none | dict | 200 | none | global |
| GET | /live | _liveness_probe (162) | none | dict | 200 | none | – |
| GET | /ready | _readiness_probe (168) | none | dict | 200 | none | – |

---

## company_crv_service (port 8006)

Same pattern as aadhar — routes on `app` in `company_crv_service/app/main.py`. `verify_internal_api_key` imported (L19) but NOT applied. Global `Exception` handler at L138.

| Method | Full path | Handler (line) | Request | Response | Success | Auth | Exceptions |
|---|---|---|---|---|---|---|---|
| GET | /health | health_check (60) | none | dict | 200 | none | global |
| GET | /api/v1/company/verify/{registration_number} | verify_company_get (73) | path | CompanyVerificationResponse | 200 | none | ValueError→400, Exception→500 |
| POST | /api/v1/company/verify | verify_company (101) | CompanyVerificationRequest | CompanyVerificationResponse | 200 | none | ValueError→400, Exception→500 |
| GET | /api/v1/company/valid-companies | get_valid_companies (123) | none | dict | 200 | none | global |
| GET | /live | _liveness_probe (162) | none | dict | 200 | none | – |
| GET | /ready | _readiness_probe (168) | none | dict | 200 | none | – |

---

## central_gateway_service (port 8000)

Reverse-proxy API Gateway built in `central_gateway_service/app/main.py` via `build_gateway_app()`. No routers included. `CORSMiddleware` with `allow_origins=["*"]` + `allow_credentials=True` (L60-64). Optional token-bucket rate-limit middleware (off by default).

| Method | Full path | Handler (line) | Behavior | Exceptions |
|---|---|---|---|---|
| GET | /health | health (87) | gateway liveness (does not check backends) | none |
| GET/POST/PUT/PATCH/DELETE/OPTIONS | /{service}/{path:path} | proxy (92, `@app.api_route`) | resolves first segment → backend base URL, forwards method/path/query/headers(−hop-by-hop, Host preserved)/body, relays response | unknown service→404, backend unreachable (httpx.RequestError)→502, rate-limit→429 (Retry-After) |

---

## registry_service (port 8010)

`registry_service/app/main.py` calls `create_registry_app()` from `libs/gdb_common/gdb_common/discovery.py` (L155). No auth. Request model `Registration` (name, url, ttl). **Built but not used at runtime** — services resolve peers via static config, not this registry.

| Method | Full path | Handler (discovery.py line) | Request | Success | Exceptions |
|---|---|---|---|---|---|
| POST | /register | register (167) | Registration | 200 | none |
| POST | /heartbeat | heartbeat (173) | Registration | 200 | not registered→404 |
| DELETE | /register | deregister (180) | Registration | 200 | none |
| GET | /resolve/{name} | resolve (186) | path | 200 | no healthy instance→404 |
| GET | /services | services (194) | none | 200 | none |
| GET | /health | health (199) | none | 200 | none |

---

## Cross-cutting notes
- Every service adds `gdb_common.install_observability(app)` middleware (correlation-id `X-Correlation-ID`, security headers, JSON logs) — not routes.
- Internal API key (`verify_internal_api_key`) is applied at router level (accounts internal, users internal, notification, payment). **aadhar and company import it but do not apply it → their `/api/v1/...` verification endpoints are unauthenticated.**
- Demo routers inside `gdb_common/auth_dependencies.py` and `auth_service/app/security/auth_dependencies.py` (L76-263) are illustrative and **not mounted** by any `main.py` — excluded.



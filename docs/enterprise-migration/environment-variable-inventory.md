# Environment Variable Inventory — Phase 0 Baseline

**Sources:** the eight per-service `config/settings.py` C# DTOs `Settings` classes, `central_gateway_service/app/config.py`, `libs/gdb_common/gdb_common/{observability.py,auth_dependencies.py,ratelimit.py}`, each `*/.env.example`, `supabase.env.example`, `frontend/.env.example`, EF Core Migrations `env.py`.

**Secret handling:** No secret VALUES appear here. Every service ships insecure DEV-DEFAULT placeholders in source (e.g. `JWT_SECRET_KEY="your-super-secret-jwt-key-change-in-production"`, `INTERNAL_API_KEY="dev-internal-api-key-change-in-prod"`, `PIN_ENCRYPTION_KEY="your-secret-encryption-key"`) — these are placeholders in code, not real credentials.

## 🔴 Live credential finding (name + location only)
The **untracked** root file `gdb-service/.env` contains real Supabase values whose password is **not** the placeholder. Affected variable names: `SUPABASE_DB_PASSWORD`, and the password embedded inside `SUPABASE_DATABASE_URL` (also `SUPABASE_DB_HOST/PORT/USER/NAME`). **Value not shown.** `.env` is git-ignored (`.gitignore:11`) so it is not committed — but it must be kept out of any commit, log, artifact, or paste, and the credential should be **rotated** (see risk register R-01). The tracked `supabase.env.example` contains only `YOUR-PASSWORD` placeholders (safe).

## Production validation
`_assert_secure_config()` runs at import in all **8** service Settings modules (accounts, transactions, users, auth, notification, aadhar, company_crv, payment_gateway). When `ENVIRONMENT=production` it raises `RuntimeError` if `INTERNAL_API_KEY` is empty/default OR `JWT_SECRET_KEY` is empty / contains `change-in-production` / `do-not-use`. **Gaps:** it does **not** validate `PIN_ENCRYPTION_KEY` (accounts) or `SECRET_KEY` (users); and `central_gateway_service` / `registry_service` have **no** guard at all.

---

### 1 — Application
| Var | Services | Source | Default (safe) | Req | Sensitive |
|---|---|---|---|---|---|
| `ENVIRONMENT` | all 8 + gdb_common | each settings.py; `auth_dependencies.py:47` | `development` | no | no |
| `DEBUG` | all 8 | each settings.py | `False` | no | no |
| `APP_NAME`/`SERVICE_NAME`, `TITLE`, `DESCRIPTION`, `APP_VERSION`/`SERVICE_VERSION` | all | each settings.py | descriptive / `1.0.0` | no | no |
| `HOST` | all 8 | each settings.py | `0.0.0.0` | no | no |
| `PORT` | all 8 | each settings.py | 8001–8008 | no | no |
| `SERVICE_PORT` | transactions (.env.example) | `transactions_service/.env.example` | `8002` | no | no |
| `ALLOWED_HOSTS` | accounts | `accounts_service/settings.py:36` | `localhost,127.0.0.1,accounts-service,*.gdb.local` | no | no |
| `API_PREFIX`/`API_V1_PREFIX`/`API_VERSION`/`API_BASE_URL` | varies | each settings.py | `/api/v1`, `v1` | no | no |
| `CORS_ALLOWED_ORIGINS`/`CORS_ORIGINS`/`CORS_CREDENTIALS`/`CORS_METHODS`/`CORS_HEADERS` | all (naming varies) | each settings.py | localhost origins | no | no |
| `MINIMUM_DEPOSIT_AMOUNT`, `MINIMUM_WITHDRAWAL_AMOUNT`, `MINIMUM_TRANSFER_AMOUNT`, `MAXIMUM_TRANSACTION_AMOUNT`, `PIN_LENGTH`, `MAX_PIN_ATTEMPTS`, `TRANSFER_LIMITS` | transactions | `transactions_service/settings.py:100-128` | see file | no | no |
| `IDEMPOTENCY_HEADER_NAME`, `IDEMPOTENCY_TTL_HOURS` | transactions | `settings.py:139-140` | `Idempotency-Key`, `24` | no | no |
| `VITE_APP_NAME`, `VITE_APP_VERSION` | frontend | `frontend/.env.example` | `GDB Banking`, `1.0.0` | no | no |

### 2 — Database
| Var | Services | Source | Default | Req | Sensitive |
|---|---|---|---|---|---|
| `DATABASE_URL` | accounts, transactions, users, auth | each settings.py; `factory.py:38` | accounts: full postgres URL w/ creds; others: `""` | no | **yes (may embed creds)** |
| `DATABASE_HOST/PORT/NAME/USER` | accounts, users, auth | each settings.py | `localhost`,`5432`,`gdb_*_db`,`postgres` | no | no |
| `DATABASE_PASSWORD` | accounts, users, auth | each settings.py | `""` (.env.example: `password`) | prod-yes | **yes** |
| `DB_HOST/PORT/USER/NAME/TIMEOUT/POOL_MIN_SIZE/POOL_MAX_SIZE` | transactions | `transactions_service/settings.py:41-48` | `localhost`,`5432`,`postgres`,`gdb_transactions_db`,`30`,`5`,`20` | no | no |
| `DB_PASSWORD` | transactions | `settings.py:44` | `password` | prod-yes | **yes** |
| `MIN_DB_POOL_SIZE`, `MAX_DB_POOL_SIZE` | accounts, auth | each settings.py | `5`, `20` | no | no |
| `EF Core Migrations_DATABASE_URL` | accounts, transactions, users, auth | `<svc>/EF Core Migrations/env.py:30` | falls back to EF Core Migrations.ini `Entity Framework Core.url` | no | **yes** |
| `SUPABASE_DATABASE_URL`, `SUPABASE_DB_HOST/PORT/USER/NAME` | supabase deploy path | `supabase.env.example`, root `.env`, `docker-compose.supabase.yml` | placeholder in example | supabase-only | mixed |
| `SUPABASE_DB_PASSWORD` | supabase deploy path | `supabase.env.example`, root `.env` | `YOUR-PASSWORD` (example) / **real in root .env** | supabase-yes | **yes — not shown** |

### 3 — Provider selection
| Var | Services | Source | Default | Req | Sensitive |
|---|---|---|---|---|---|
| `DATABASE_PROVIDER` | accounts, transactions, users, auth | each settings.py (~L35-43) | `postgres` (`sqlite\|mysql\|postgres\|supabase\|inmemory`) | no | no |

### 4 — Authentication (JWT)
| Var | Services | Source | Default | Req | Sensitive |
|---|---|---|---|---|---|
| `JWT_SECRET_KEY` | accounts, transactions, users, auth | each settings.py | placeholder | prod-yes | **yes — [sensitive]** |
| `JWT_ALGORITHM` | accounts, transactions, users, auth | each settings.py | `HS256` | no | no |
| `JWT_PUBLIC_KEY` | accounts, transactions, users, auth | each settings.py | `""` | no | no (public) |
| `JWT_PRIVATE_KEY` | auth only | `auth_service/settings.py:74` | `""` | prod (RS256) | **yes — [sensitive]** |
| `JWT_EXPIRY_MINUTES` | auth | `auth_service/settings.py:78` | `30` | no | no |
| `JWT_EXPIRATION_HOURS` | transactions | `transactions_service/settings.py:94` | `24` | no | no |
| `ACCESS_TOKEN_EXPIRE_MINUTES` | accounts, users | each settings.py | `30` | no | no |
| `SECRET_KEY` | users | `users_service/settings.py:46` | placeholder | prod-yes | **yes — [sensitive]** (NOT checked by guard) |
| `ALGORITHM` | users | `users_service/settings.py:47` | `HS256` | no | no |
| `DISABLE_AUTH` | accounts | `accounts_service/settings.py:64` | `False` | no | **dangerous if true** |

### 5 — Authorization (RBAC)
| Var | Services | Source | Default | Req | Sensitive |
|---|---|---|---|---|---|
| `ALLOWED_ROLES` | transactions | `transactions_service/settings.py:100` | WITHDRAW/DEPOSIT/TRANSFER→[MANAGER,TELLER], VIEW_LOGS/MANAGE_LIMITS→[ADMIN] | no | no |

Role enforcement also lives in `gdb_common/auth_dependencies.py`, which reads `ENVIRONMENT` to decide whether a dev bypass applies (production ignores it, `auth_dependencies.py:97-98`).

### 6 — Internal service security
| Var | Services | Source | Default | Req | Sensitive |
|---|---|---|---|---|---|
| `INTERNAL_API_KEY` | all 8 (validated in every guard) | each settings.py | `dev-internal-api-key-change-in-prod` | prod-yes | **yes — [sensitive]** |

### 7 — Service URLs
| Var | Services | Source | Default | Req | Sensitive |
|---|---|---|---|---|---|
| `ACCOUNTS_SERVICE_URL` / `ACCOUNT_SERVICE_URL` | all 8 | each settings.py | `http://localhost:8001` | no | no |
| `TRANSACTIONS_SERVICE_URL` / `TRANSACTION_SERVICE_URL` | all 8 | each settings.py | `:8002` | no | no |
| `USERS_SERVICE_URL` / `USER_SERVICE_URL` | all 8 | each settings.py | `:8003` | no | no |
| `AUTH_SERVICE_URL` | all 8 | each settings.py | `:8004` | no | no |
| `AADHAR_SERVICE_URL` | all 8 | each settings.py | `:8005` | no | no |
| `COMPANY_SERVICE_URL` | all 8 | each settings.py | `:8006` | no | no |
| `NOTIFICATION_SERVICE_URL` | all 8 | each settings.py | `:8007` | no | no |
| `PAYMENT_GATEWAY_SERVICE_URL` | all 8 | each settings.py | `:8008` | no | no |
| `ACCOUNT_SERVICE_TIMEOUT` / `USER_SERVICE_TIMEOUT` | accounts,transactions / auth | each settings.py | `10` | no | no |
| `VITE_*_SERVICE_URL` (8 incl. `VITE_PAYMENT_GATEWAY_URL`, `VITE_COMPANY_CRV_SERVICE_URL`) | frontend | `frontend/.env.example` | `http://localhost:800x` | no | no |

Both **plural and legacy-singular** aliases exist for accounts/transactions/users — a backward-compat surface that must be preserved.

### 8 — Gateway
| Var | Services | Source | Default | Req | Sensitive |
|---|---|---|---|---|---|
| `GATEWAY_<NAME>_URL` (`GATEWAY_ACCOUNTS_URL`, `_TRANSACTIONS_URL`, `_USERS_URL`, `_AUTH_URL`, `_AADHAR_URL`, `_COMPANY_URL`, `_NOTIFICATION_URL`, `_PAYMENT_URL`) | central_gateway | `central_gateway_service/app/config.py:26` (`backends_from_env`) | falls back to `DEFAULT_BACKENDS` localhost ports | no | no |

### 9 — Logging
| Var | Services | Source | Default | Req | Sensitive |
|---|---|---|---|---|---|
| `LOG_LEVEL` | all 8 | each settings.py | `INFO` | no | no |
| `LOG_FILE` | users, auth | each settings.py | `logs/<svc>.log` | no | no |
| `LOG_DIR`, `LOG_FILE_FORMAT` | transactions | `transactions_service/settings.py:119-120` | `./logs/transactions`, `%Y-%m-%d` | no | no |
| `LOG_FORMAT` | any using gdb_common observability | `observability.py:109` | `""` → `json` for JSON logs | no | no |
| `DATA_FILE` | notification | `notification_service/settings.py:54` | `data/notifications.json` | no | no |

### 10 — Observability
`LOG_FORMAT` (above) is the **only** observability toggle read from env. **No `OTEL_*`/`OTLP_*`/tracing/metrics env vars exist** (grep for `OTEL|OTLP|TRACE` → nothing). Distributed tracing/metrics exporters: **Not implemented.** `transactions_service/app/correlation.py` exists for correlation-ID propagation but is not env-driven.

### 11 — Rate limiting
| Var | Services | Source | Default | Req | Sensitive |
|---|---|---|---|---|---|
| `GATEWAY_RATE_LIMIT_PER_MIN` | central_gateway | `central_gateway_service/app/config.py:40` | `0` (**disabled**) | no | no |
| `GATEWAY_RATE_BURST` | central_gateway | `central_gateway_service/app/config.py:46` | = per-min value | no | no |

Limiter implementation is `gdb_common/ratelimit.py`; it takes no other env vars.

### 12 — Encryption
| Var | Services | Source | Default | Req | Sensitive |
|---|---|---|---|---|---|
| `PIN_ENCRYPTION_KEY` | accounts | `accounts_service/settings.py:56` (.env.example: `dev-pin-encryption-key-12345`) | `your-secret-encryption-key` | prod-yes | **yes — [sensitive]** |

Used by `accounts_service/app/utils/encryption.py` (`EncryptionManager`, Fernet + HMAC blind index). ⚠️ **Not validated** by `_assert_secure_config`, so an insecure default PIN key could reach production.

### 13 — Third-party integrations
**None.** No Stripe/Twilio/SendGrid/AWS/SMTP keys. The "third-party" services (aadhar, company_crv, payment_gateway) are **simulated in-repo** and reached via internal `*_SERVICE_URL` + `INTERNAL_API_KEY` (categories 6 & 7). Supabase (category 2) is the only real external backend. → **Not implemented** as a distinct credential category.

---

## "Not implemented" confirmations
- Observability tracing/metrics env — none beyond `LOG_FORMAT`.
- External third-party credentials — none (simulated services + Supabase only).
- Production guard — absent in `central_gateway_service` and `registry_service`; and does not cover `PIN_ENCRYPTION_KEY` or users `SECRET_KEY`.



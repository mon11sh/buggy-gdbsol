# Database Schema Baseline — Phase 0

**Stateful services:** accounts (`gdb_accounts_db`), transactions (`gdb_transactions_db`), users (`gdb_users_db`), auth (`gdb_auth_db`).
**Stateless (no schema):** aadhar, company_crv, notification (writes a JSON file, no DB), central_payment_gateway, central_gateway, registry.
**Do not modify any schema during Phase 0.** This is a read-only baseline.

## ⚠️ Source-of-truth finding (read first)
What creates the schema at startup depends on the provider:
- **accounts / users / auth** — all SQL backends run `Base.metadata.create_all` from `orm_models.py` (`Entity Framework CoreProvider.init()`). The raw `*_schema.sql` files are **NOT** executed and have **drifted** from the ORM. EF Core Migrations baselines exist but are **not** run at startup.
- **transactions** — **MIXED**: postgres/supabase (the **default**) run the raw `transactions_schema.sql` via `AsyncpgProvider`; sqlite/mysql run `create_all`.

| Service | Startup init path | Authoritative schema | EF Core Migrations | Raw `*_schema.sql` |
|---|---|---|---|---|
| accounts | `Entity Framework CoreProvider.init` (all SQL) | **create_all() (ORM)** | mirror, not run | dead/drifted |
| users | `Entity Framework CoreProvider.init` (all SQL) | **create_all() (ORM)** | mirror, not run | dead/drifted (table name + JSONB + FK differ) |
| auth | `Entity Framework CoreProvider.init` (all SQL) | **create_all() (ORM)** | mirror, not run | dead/drifted |
| transactions | `AsyncpgProvider` (pg/supabase, default) OR `Entity Framework CoreProvider` (sqlite/mysql) | **MIXED: raw SQL on postgres, create_all on sqlite/mysql** | mirror, not run | **LIVE on postgres/supabase** |

---

## accounts_service — `gdb_accounts_db`
ORM `app/database/orm_models.py`; raw `accounts_schema.sql`; EF Core Migrations `cfd3115280bf`; seed `seed_data.py`.

### `accounts` — `AccountORM` (orm_models.py:44-87)
PK `id` (Integer, autoinc). Unique+index `account_number`. No FK.
Columns: `id` PK; `account_number` Int unique index (app-assigned, floor 1000); `account_type` String(10); `name` String(255); `pin_hash` String(255); `balance` Numeric(15,2) default 0.00; `privilege` String(10); `bank_name` String(255) default "Global Digital Bank"; `bank_branch` String(255) default "Main Branch"; `ifsc_code` String(20) default "GDB0000001"; `is_active` Boolean default True; `activated_date` DateTime default utcnow; `closed_date` DateTime nullable; `created_at` DateTime utcnow; `updated_at` DateTime utcnow onupdate.

### `savings_account_details` — `SavingsAccountDetailsORM` (orm_models.py:90-124)
PK `id`. Unique+index `account_number`, unique+index `aadhar_hash` String(64). **FK** `account_number → accounts.account_number` **ON DELETE CASCADE** (L103).
Columns: `id` PK; `account_number` Int unique FK; `date_of_birth` Date; `gender` String(10); `phone_no` String(20); `aadhar_number` String(255) **encrypted at rest, no unique**; `aadhar_hash` String(64) unique index (blind index); `created_at`/`updated_at`. `account_to_dict` decrypts Aadhaar on read (L186-192).

### `current_account_details` — `CurrentAccountDetailsORM` (orm_models.py:127-154)
PK `id`. Unique+index `account_number`, unique+index `registration_no` String(50). **FK** `account_number → accounts.account_number` **ON DELETE CASCADE** (L139).
Columns: `id` PK; `account_number` Int unique FK; `company_name` String(255); `website` String(255) nullable; `registration_no` String(50) unique; `created_at`/`updated_at`.

**Source of truth:** create_all (all SQL backends) + seed of 2 accounts when empty (`seed_data.py:seed_default_accounts`). Raw `accounts_schema.sql` (BIGSERIAL + `account_number_seq` START 1000 + CHECK constraints + `gender_enum` + `account_summary` VIEW + `update_updated_at_column()` triggers) is **dead/drifted** — none of that exists in the create_all path.

---

## transactions_service — `gdb_transactions_db`
ORM `app/database/orm_models.py`; raw `transactions_schema.sql` (LIVE on postgres); EF Core Migrations `8beaf28da5fa`; seed `seed_data.py` + inline `db.py`.

### `fund_transfers` — `FundTransferORM` (orm_models.py:24-41)
PK `id`. Indexes `from_account`, `to_account`, `created_at`. No unique, no FK.
Columns: `id` PK; `from_account` Int index; `to_account` Int index; `transfer_amount` Numeric(15,2); `transfer_mode` String(20); `created_at` DateTime index; `updated_at`. Raw SQL adds CHECK `from_account <> to_account` + CHECK mode IN (NEFT,RTGS,IMPS,UPI) — **not in ORM**.

### `transaction_logging` — `TransactionLoggingORM` (orm_models.py:44-65)
PK `id`. Indexes `account_number`, `transaction_type`, `created_at`. No FK/unique.
Columns: `id` PK; `account_number` Int index; `amount` Numeric(15,2); `transaction_type` String(20) index; `reference_id` Int nullable; `description` String(255) nullable; `mode` String(20) nullable; `status` String(20) default "SUCCESS"; `created_at` index; `updated_at`.

### `transfer_limits` — `TransferLimitORM` (orm_models.py:68-83)
**PK `privilege` (natural key)** String(20). No FK.
Columns: `privilege` PK; `daily_limit` Numeric(15,2); `per_transaction_limit` Numeric(15,2); `created_at`/`updated_at`. Seeded PREMIUM(100000/50000), GOLD(50000/25000), SILVER(25000/12500).

### `idempotency_keys` — `IdempotencyKeyORM` (orm_models.py:86-103)
**PK `idempotency_key` (natural key)** String(100).
Columns: `idempotency_key` PK; `operation` String(20); `status` String(20) default "IN_PROGRESS"; `response_json` Text nullable; `created_at`/`updated_at`.

**Source of truth — MIXED:** postgres/supabase (default) → raw `transactions_schema.sql` via `bootstrap_asyncpg()` (db.py:136-209) if `transfer_limits` missing, then seed. sqlite/mysql → create_all + seed. Seed content identical on both paths (`seed_data.py:16-65` / `db.py:157-208`): the 3 transfer_limits rows + sample logs (deposit 60000 to 1001; transfer 10000 from 1002→1001 with two logging legs).

---

## users_service — `gdb_users_db`
ORM `app/database/orm_models.py`; raw `users_schema.sql`; EF Core Migrations `2943927e5a89`; seed `seed_data.py`.

### `users` — `UserORM` (orm_models.py:22-37)
PK `user_id` (Int autoinc). Unique+index `login_id` String(50). No FK.
Columns: `user_id` PK; `username` String(255); `login_id` String(50) unique; `password` String(255); `role` String(20) default "MANAGER"; `is_active` Boolean default True; `created_at`/`updated_at`.

### `user_audit_logs` — `AuditLogORM` (orm_models.py:40-51)
PK `log_id` (Int autoinc). No FK, no unique, no index.
Columns: `log_id` PK; `user_id` Int nullable; `action` String(30); `old_data` Text nullable; `new_data` Text nullable; `performed_by` String(50) nullable; `timestamp` DateTime default utcnow.

**Source of truth:** create_all + seed of 10 users (`seed_data.py:11-30`, default password `Welcome@1`). 🔴 **Significant drift:** raw `users_schema.sql` defines a differently-named table `user_audit_log` (singular) with PK `audit_id`, `old_data/new_data` as **JSONB**, **FK `user_id → users.user_id` ON DELETE SET NULL**, `audit_action_enum`, indexes, no `performed_by`. The raw SQL is not run, so the ORM shape (`user_audit_logs`, Text, `performed_by`, no FK) is authoritative.

---

## auth_service — `gdb_auth_db`
ORM `app/database/orm_models.py`; raw `auth_schema.sql`; EF Core Migrations `e75fd9cb5b96`. No seed.

### `auth_tokens` — `AuthTokenORM` (orm_models.py:43-60)
**PK `id` String(36) uuid4 (not native UUID).** Unique+index `token_jti`. Indexes `user_id`, `expires_at`, `is_revoked`.
Columns: `id` PK; `user_id` Int index; `login_id` String(255); `token_jti` String(255) unique index; `issued_at` DateTime; `expires_at` DateTime index; `is_revoked` Boolean default False index; `created_at` DateTime (`_utcnow`). Raw SQL adds CHECK `expires_at > issued_at` — not in ORM.

### `auth_audit_logs` — `AuthAuditLogORM` (orm_models.py:63-83)
PK `id` String(36). No unique. Indexes `login_id`, `user_id`, `action`, `created_at`. No FK.
Columns: `id` PK; `login_id` String(255) index; `user_id` Int nullable index; `action` String(30) index; `reason` String(500) nullable; `ip_address` String(45) nullable; `user_agent` String(1000) nullable; `created_at` DateTime index.

**Source of truth:** create_all (no seed). Raw `auth_schema.sql` (native UUID/INET/ENUM + TIMESTAMPTZ + views + `cleanup_expired_tokens()`) is dead/drifted; the ORM uses portable types.

---

## Constraint summary (backward-compat critical)
- **Unique:** `accounts.account_number`; `savings_account_details.aadhar_hash` + `account_number`; `current_account_details.registration_no` + `account_number`; `users.login_id`; `auth_tokens.token_jti`; `transfer_limits.privilege` (PK); `idempotency_keys.idempotency_key` (PK).
- **FK (ON DELETE CASCADE):** `savings_account_details.account_number → accounts.account_number`; `current_account_details.account_number → accounts.account_number`. (No FK on transactions/auth/users ORM tables.)
- **Natural PKs:** `transfer_limits.privilege`; `idempotency_keys.idempotency_key`.
- **Account numbers start at 1000** (`ACCOUNT_NUMBER_START`, orm_models.py:37) — app-level counter that replaces the Postgres `account_number_seq`.

## Drift/risk callouts (grounded)
1. transactions is the only **MIXED-schema** service (raw SQL with CHECKs on postgres vs no-CHECK create_all on sqlite/mysql).
2. users `user_audit_logs` ORM vs `user_audit_log` raw-SQL divergence (name/type/FK).
3. accounts Aadhaar: ORM encrypts + blind-index; raw SQL still plaintext `VARCHAR(12) UNIQUE`. Response masks to `********NNNN`.
4. `transfer_mode` mismatch: `FundTransferCreate` DTO accepts `CHEQUE`, but DB CHECK/enum (postgres) allows only NEFT/RTGS/IMPS/UPI → a CHEQUE transfer fails on postgres, passes on sqlite/mysql.
5. EF Core Migrations baselines exist for all four but are **not executed at startup** → runtime schema is unmanaged by migrations.



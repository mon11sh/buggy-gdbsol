# transactions_service — Code-Level Flow

> Owns **money movement** — deposits, withdrawals, fund transfers — plus the
> local transaction **ledger + audit logs**, per-privilege **transfer limits**,
> and **idempotency** keys. Port **8002**, database **`gdb_transactions_db`**.

The primary write path documented here is the **fund transfer**
(`POST /api/v1/transactions/transfer`) — the richest flow: a cross-service SAGA
that reserves an idempotency key, validates both accounts + PIN + limits, gets
**payment-gateway approval before any money moves**, then debits the source and
credits the destination (with a compensating reversal on credit failure) and
records the ledger + both log legs in **one local commit**. Deposit and
withdrawal are simpler single-account variants of the same skeleton.

---

## 1. Layered architecture (dependency rule points inward)

```mermaid
flowchart TB
    subgraph API["API layer"]
        R["api/frontend_routes.py<br/>transfer()"]
        R2["api/transfer_routes.py<br/>transfer_funds() (alt)"]
        D["dto/transaction_dto.py<br/>TransferRequest"]
    end
    subgraph APP["Application layer"]
        UC["services/transfer_service.py<br/>TransferService.process_transfer()"]
        VAL["validation/validators.py<br/>Amount·Balance·PIN·Transfer·TransferLimit"]
        UOWC["services/unit_of_work.py<br/>TransactionUnitOfWork (contract)"]
    end
    subgraph DOM["Domain layer (framework-free)"]
        RULES["domain/rules.py<br/>ensure_* (amount·balance·pin·limits·different)"]
        PORTS["domain/ports.py<br/>AccountServicePort · PaymentGatewayPort · NotificationPort"]
        MOD["domain/models/*<br/>Money · FundTransfer · TransferLimit · enums"]
    end
    subgraph INFRA["Infrastructure"]
        UOW["infrastructure/persistence/unit_of_work.py<br/>Entity Framework CoreUnitOfWork (owns AsyncSession)"]
        REPO["…/repositories/Entity Framework Core_repositories.py<br/>transactions · logs · limits · idempotency"]
        ORM["…/orm_models.py<br/>FundTransferORM · TransactionLoggingORM · TransferLimitORM · IdempotencyKeyORM"]
    end
    CR["composition/composition_root.py<br/>TransactionsCompositionRoot.transfer_service()"]
    ACC["integration/account_service_client.py → accounts_service :8001 (internal API)"]
    PG["integration/payment_gateway_client.py → central_payment_gateway"]
    NOT["integration/notification_client.py → notification_service"]
    DB[("gdb_transactions_db")]

    R --> D
    R --> CR
    CR --> UC
    UC --> VAL --> RULES
    UC --> UOWC
    UC --> PORTS
    UC --> MOD
    UOWC -. implemented by .-> UOW
    PORTS -. implemented by .-> ACC & PG & NOT
    UOW --> REPO --> ORM --> DB
```

## 2. Primary flow — fund transfer (code-level sequence)

```mermaid
sequenceDiagram
    autonumber
    actor C as Client (MANAGER/TELLER)
    participant R as frontend_routes.<br/>transfer
    participant DI as providers.<br/>get_fund_transfer_service
    participant UC as TransferService.<br/>process_transfer
    participant UOW as Entity Framework CoreUnitOfWork
    participant IDEM as idempotency repo
    participant ACC as AccountServiceClient → accounts_service
    participant PG as PaymentGatewayClient → gateway
    participant DB as gdb_transactions_db
    participant NOT as NotificationClient

    C->>R: POST /api/v1/transactions/transfer (JWT + TransferRequest)
    Note over R: ASP.NET Core Web API validates body → TransferRequest (422 if invalid)
    R->>DI: resolve TransferService (per-request UoW built)
    R->>UC: process_transfer(from,to,amount,pin,mode,idempotency_key)
    rect rgb(235,235,255)
    UC->>IDEM: get_completed_response(key) %% replay?
    IDEM-->>UC: None
    UC->>IDEM: reserve(key,"TRANSFER") %% insert-first
    IDEM-->>UC: True (409 if already in progress)
    UC->>UOW: commit() %% reservation durable
    end
    UC->>ACC: validate_account(from) / validate_account(to)
    ACC-->>UC: account payloads (404/400 if missing/inactive)
    UC->>UC: TransferValidator.validate_different_accounts
    UC->>UC: PINValidator.validate_pin_format
    UC->>ACC: verify_pin(from, pin) %% 401 on mismatch
    UC->>UC: AmountValidator + BalanceValidator
    UC->>IDEM: limits.get_daily_used_amount / get_daily_transaction_count
    UC->>UC: TransferLimitValidator (daily + count + per-txn from DB rule)
    rect rgb(255,245,230)
    UC->>PG: validate_payment(from,to,amount,mode) %% BEFORE any money moves
    PG-->>UC: approved? (402→PaymentProcessingError 400 if rejected)
    end
    rect rgb(235,255,235)
    UC->>ACC: debit_account(from, amount)
    UC->>ACC: credit_account(to, amount)
    Note over UC,ACC: credit fails → credit_account(from) reversal → TransferFailedException
    UC->>DB: transactions.create_transaction (fund_transfers)
    UC->>DB: logs.log_to_database (TRANSFER for sender)
    UC->>DB: logs.log_to_database (DEPOSIT for recipient)
    UC->>UOW: commit() %% ledger + both logs, ONE commit
    end
    UC->>UC: logs.log_to_file (append-only file)
    UC->>NOT: send_notification(from) / send_notification(to) %% best-effort
    UC->>IDEM: complete(key, result)
    UC->>UOW: commit()
    UC-->>R: result dict (status SUCCESS, transaction_id, balances)
    R-->>C: 201 Created (transfer result)
    Note over DI: finally → uow.aclose() releases the session
```

## 3. Layer-by-layer walkthrough

| # | Layer | File · symbol | What happens |
|---|---|---|---|
| 1 | **API** | [frontend_routes.py](../../transactions_service/app/api/frontend_routes.py) · `transfer` | `POST /api/v1/transactions/transfer` (router prefix `/api/v1/transactions`); auth `require_manager_or_teller_dependency`; parses `TransferRequest` body; resolves `TransferMode` (falls back to `NEFT` on unknown); reads `Idempotency-Key` header; returns the service result (201). Thin — no business logic. |
| 1b | **API (alt)** | [transfer_routes.py](../../transactions_service/app/api/transfer_routes.py) · `transfer_funds` | Equivalent `POST /api/v1/transfers` using **query params** instead of a JSON body; same auth + same `TransferService`. |
| 2 | **DTO** | [transaction_dto.py](../../transactions_service/app/dto/transaction_dto.py) · `TransferRequest` | `from_account, to_account, amount (>0), pin, transfer_mode` (defaults `NEFT`). `FundTransferCreate`/`Response` are the ledger record schemas. |
| 3 | **DI** | [providers.py](../../transactions_service/app/dependencies/providers.py) · `get_fund_transfer_service` | Asks the composition root for a per-request session-owning UoW, builds `TransferService`, `uow.aclose()` in `finally`. |
| 4 | **Composition** | [composition_root.py](../../transactions_service/app/composition/composition_root.py) · `TransactionsCompositionRoot.transfer_service` | Wires the UoW + the three outbound ports (account/gateway/notifier singletons) into `TransferService`. |
| 5 | **Use case** | [transfer_service.py](../../transactions_service/app/services/transfer_service.py) · `TransferService.process_transfer` | The saga: idempotency reserve → validate accounts → PIN → amount → balance → daily/count/per-txn limits → **gateway approval** → debit → credit (reversal on failure) → ledger + logs (one commit) → notifications → idempotency complete. |
| 6 | **Validators** | [validators.py](../../transactions_service/app/validation/validators.py) · `Amount/Balance/PIN/Transfer/TransferLimit Validator` | Thin facade resolving `settings` limits and delegating to the domain rules. |
| 7 | **Domain rules** | [rules.py](../../transactions_service/app/domain/rules.py) · `ensure_*` | Pure functions raising the exact money-movement exceptions: amount bounds, sufficient balance, PIN format, daily amount/count, per-transaction ceiling, different accounts. |
| 8 | **Ports** | [ports.py](../../transactions_service/app/domain/ports.py) · `AccountServicePort · PaymentGatewayPort · NotificationPort` | Abstractions the use case depends on; concrete HTTP clients implement them. |
| 9 | **Domain models** | [fund_transfer.py](../../transactions_service/app/domain/models/fund_transfer.py), [transfer_limit.py](../../transactions_service/app/domain/models/transfer_limit.py), [enums.py](../../transactions_service/app/domain/models/enums.py) | `FundTransfer` entity, `TransferLimit`/`Money` value objects, `TransactionType`/`TransferMode`/`PrivilegeLevel` enums. |
| 10 | **Unit of Work** | [unit_of_work.py](../../transactions_service/app/infrastructure/persistence/unit_of_work.py) · `Entity Framework CoreUnitOfWork` | Owns the `AsyncSession`; binds the four repos (`.transactions/.logs/.limits/.idempotency`); commits per logical step; `aclose()` releases. In-memory variant treats commit/rollback as no-ops. |
| 11 | **Repositories** | [Entity Framework Core_repositories.py](../../transactions_service/app/infrastructure/persistence/repositories/Entity Framework Core_repositories.py) · `Entity Framework Core{Transaction,TransactionLog,TransferLimit,Idempotency}Repository` | Write through the UoW session **without committing**; `Idempotency.reserve` is insert-first (duplicate → `IntegrityError` → rollback → `False`; real error fails CLOSED → `DatabaseException`). |
| 12 | **Integration** | [account_service_client.py](../../transactions_service/app/integration/account_service_client.py), [payment_gateway_client.py](../../transactions_service/app/integration/payment_gateway_client.py), [notification_client.py](../../transactions_service/app/integration/notification_client.py) | HTTP clients (internal API key + traceparent + correlation id). Account calls go through a `CircuitBreaker`; gateway resolves any failure to `False` (fail-closed); notifications swallow errors. |
| 13 | **ORM / DB** | [orm_models.py](../../transactions_service/app/infrastructure/persistence/orm_models.py) | Writes `fund_transfers`, `transaction_logging`, `idempotency_keys`; reads `transfer_limits`. |

## 4. Endpoint map

| Method | Route | Handler | Auth |
|---|---|---|---|
| POST | `/api/v1/transactions/transfer` | `frontend_routes.transfer` | MANAGER / TELLER |
| POST | `/api/v1/transactions/deposit` | `frontend_routes.deposit` | MANAGER / TELLER |
| POST | `/api/v1/transactions/withdraw` | `frontend_routes.withdraw` | MANAGER / TELLER |
| GET | `/api/v1/transactions` | `frontend_routes.get_all_transactions` | ADMIN / TELLER / MANAGER |
| GET | `/api/v1/transactions/account/{account_number}` | `frontend_routes.get_transactions_by_account` | any authenticated user |
| POST | `/api/v1/transfers` | `transfer_routes.transfer_funds` (query-param alt) | MANAGER / TELLER |
| POST | `/api/v1/deposits` · `/api/v1/withdrawals` | deposit / withdraw routers | MANAGER / TELLER |
| — | `/api/v1/transfer-limits` | transfer-limit CRUD | (see transfer_limit_routes) |
| — | `/api/v1/transaction-logs` | log queries | (see transaction_log_routes) |
| GET | `/api/v1/health` · `/ready` · `/live` | health/readiness/liveness | none |

## 5. Business rules enforced

```mermaid
flowchart LR
    K["Idempotency key reserved (insert-first)"] --> G{Execute transfer?}
    A["Both accounts exist & active"] --> G
    B["Accounts differ"] --> G
    P["PIN valid (format + account-service verify)"] --> G
    M["Amount > 0 and ≤ max"] --> G
    S["Sufficient source balance"] --> G
    L["Daily amount + daily count + per-txn limit (by privilege)"] --> G
    PG["Payment gateway APPROVES (before any money moves)"] --> G
    G -->|all pass| MOVE["debit → credit (reverse on credit fail) → ledger+logs ONE commit → complete key"]
    G -->|any fail| ERR["raise → release key if money not yet moved → HTTP error"]
```

1. **Idempotency (insert-first)** — a COMPLETED key replays its stored result; a
   still-in-progress duplicate is rejected **409**; the reservation is committed
   before work starts and **released** only if the request fails *before* money
   moved (`money_moved` guard).
2. **Both accounts valid & active** — `validate_account(from)` and
   `validate_account(to)` against accounts_service (`404`/`400`).
3. **Different accounts** — `ensure_different_accounts` (`400`).
4. **PIN** — format checked locally, then `verify_pin` against accounts_service
   (`401` on mismatch).
5. **Amount** — `> 0` and `≤ MAXIMUM_TRANSACTION_AMOUNT` (`400`).
6. **Sufficient balance** — source `balance ≥ amount` (`400`).
7. **Limits by privilege** — daily cumulative amount, daily transfer count, and a
   **per-transaction ceiling** (preferring the DB `transfer_limits` rule, falling
   back to `settings.TRANSFER_LIMITS`) (`400`).
8. **Gateway approval first** — `validate_payment` must return true **before any
   debit/credit**; rejection raises `PaymentProcessingError` (`400`,
   `PAYMENT_GATEWAY_ERROR`).
9. **Atomic money movement by compensation** — debit then credit are two
   independent account-service calls; if the credit fails after a successful
   debit, the debit is **reversed** (refund) and `TransferFailedException` is
   raised. Once both legs settle (`money_moved = True`), a later ledger/log
   failure must **not** release the idempotency key.
10. **Single local commit for the record** — the `fund_transfers` row + both
    `transaction_logging` legs (TRANSFER for sender, DEPOSIT for recipient) are
    written and committed **once**.
11. **Best-effort side effects** — file log and customer notifications never
    block or fail the transfer.

## 6. Exception points → HTTP

| Raised in | Condition | Exception | HTTP |
|---|---|---|---|
| ASP.NET Core Web API/C# DTOs | Malformed body / bad field | validation error | **422** |
| `AccountServiceClient.verify_pin` / `PINValidator` | Wrong or malformed PIN | `InvalidPINException` | **401** |
| `AccountServiceClient.validate_account` | Account missing | `AccountNotFoundException` | **404** |
| `AccountServiceClient.validate_account` | Account inactive | `AccountNotActiveException` | **400** |
| `ensure_different_accounts` | Same source & dest | `SameAccountTransferException` | **400** |
| `ensure_amount_within_bounds` | Amount ≤ 0 or > max | `InvalidAmountException` | **400** |
| `ensure_sufficient_balance` | Balance < amount | `InsufficientFundsException` | **400** |
| `TransferLimitValidator` | Daily amount / per-txn ceiling exceeded | `TransferLimitExceededException` | **400** |
| `TransferLimitValidator` | Daily transfer count exceeded | `DailyTransactionCountExceededException` | **400** |
| gateway step | Gateway rejects (or unreachable → False) | `PaymentProcessingError` (`PAYMENT_GATEWAY_ERROR`) | **400** |
| idempotency step | Duplicate key still in progress | `IdempotencyException` | **409** |
| account/gateway clients | Downstream down / circuit open | `ServiceUnavailableException` | **503** |
| credit leg | Credit failed after debit (debit reversed) | `TransferFailedException` | **400** |
| `process_transfer` `except Exception` | Anything unexpected (wrapped) | `TransferFailedException` | **400** |
| route `except Exception` / global handler | Non-`TransactionException` escapes | generic | **500** |

*(All service errors subclass `TransactionException`, which carries its own
`http_code`/`error_code`. Both the route's `except TransactionException` and the
app-level `@app.exception_handler(TransactionException)` in
[main.py](../../transactions_service/app/main.py) render
`{error_code, message}` at `exc.http_code`. Note: the use case wraps most
unexpected errors into `TransferFailedException` (400), so a raw **500** is rare —
it appears only for errors that escape as a plain `Exception`.)*

## 7. Database

```mermaid
erDiagram
    fund_transfers ||--o{ transaction_logging : "reference_id → id"
    transfer_limits {
        string privilege PK "PREMIUM/GOLD/SILVER"
        numeric daily_limit "15,2"
        numeric per_transaction_limit "15,2"
        datetime created_at
        datetime updated_at
    }
    fund_transfers {
        int id PK
        int from_account "indexed"
        int to_account "indexed"
        numeric transfer_amount "15,2"
        string transfer_mode "NEFT/RTGS/IMPS/UPI/CHEQUE"
        datetime created_at "indexed"
        datetime updated_at
    }
    transaction_logging {
        int id PK
        int account_number "indexed"
        numeric amount "15,2"
        string transaction_type "DEPOSIT/WITHDRAW/TRANSFER"
        int reference_id "→ fund_transfers.id"
        string description
        string mode
        string status "SUCCESS"
        datetime created_at "indexed"
        datetime updated_at
    }
    idempotency_keys {
        string idempotency_key PK
        string operation "TRANSFER/DEPOSIT/WITHDRAW"
        string status "IN_PROGRESS/COMPLETED"
        text response_json "cached result"
        datetime created_at
        datetime updated_at
    }
```

Tables written by the transfer flow:

- **`idempotency_keys`** (`IdempotencyKeyORM`) — one row per key: inserted
  `IN_PROGRESS` at reserve (committed immediately), updated to `COMPLETED` with
  the cached `response_json` at the end, or **deleted** on release when the
  request fails before money moved.
- **`fund_transfers`** (`FundTransferORM`) — the ledger record: source,
  destination, amount, mode, timestamps. Its `id` becomes the `reference_id`
  linking the two log legs.
- **`transaction_logging`** (`TransactionLoggingORM`) — two rows per transfer: a
  `TRANSFER` leg for the sender and a `DEPOSIT` leg for the recipient, both
  referencing the transfer id. Written + committed together with the ledger row.
- **`transfer_limits`** (`TransferLimitORM`) — **read** (not written) during the
  transfer to source the per-transaction ceiling by privilege (with a
  `settings.TRANSFER_LIMITS` fallback).
- Same portable schema on sqlite/mysql/postgres/supabase (portable column types
  in `orm_models.py`; the in-memory provider mirrors the same repository
  contracts).

---

**Related:** [accounts_service flow](accounts-service-flow.md) · [system architecture](../architecture/system-architecture.md)



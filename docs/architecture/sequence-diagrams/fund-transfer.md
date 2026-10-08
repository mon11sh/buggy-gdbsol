# Sequence — Fund Transfer (saga)

A transfer is a **cross-service saga with compensation** and **idempotency**.
Money moves through `accounts_service` (debit → credit, reversed on failure);
the local ledger + logs are written in **one** Unit-of-Work commit; both parties
are notified best-effort. Source: `transactions_service` `TransferService`.

```mermaid
sequenceDiagram
    autonumber
    actor U as Client (staff)
    participant GW as central_gateway
    participant TX as transactions_service<br/>(TransferService)
    participant DB as gdb_transactions_db (UoW)
    participant AC as accounts_service
    participant PG as central_payment_gateway
    participant N as notification_service

    U->>GW: POST /api/v1/transactions/transfer (Bearer, Idempotency-Key)
    GW->>TX: proxy (+correlation, traceparent)

    rect rgb(240,240,255)
    Note over TX,DB: STEP 0 — Idempotency (insert-first)
    TX->>DB: idempotency.get_completed_response(key)
    alt key already completed
        DB-->>TX: stored result
        TX-->>U: 200 (replay) — transfer NOT re-executed
    else reserve
        TX->>DB: idempotency.reserve(key); uow.commit()
    end
    end

    Note over TX,AC: STEP 1–6 — Validate (domain rules + account state)
    TX->>AC: validate_account(from), validate_account(to)
    AC-->>TX: account data (status, balance, privilege)
    TX->>TX: different-accounts, PIN format, amount, balance,<br/>daily + per-transaction limits (domain rules)
    TX->>AC: verify_pin(from, pin)

    Note over TX,PG: STEP 6.5 — Gateway authorization (before any money moves)
    TX->>PG: validate_payment(from, to, amount, mode)
    PG-->>TX: approved? 
    alt rejected
        TX-->>U: 402 PaymentProcessingError (nothing moved)
    end

    rect rgb(255,245,235)
    Note over TX,AC: STEP 7 — Move money (compensating saga)
    TX->>AC: debit_account(from, amount)
    AC-->>TX: new balance
    TX->>AC: credit_account(to, amount)
    alt credit fails
        TX->>AC: credit_account(from, amount)  %% reverse the debit
        TX-->>U: 500 TransferFailed — source refunded
    else credit ok
        AC-->>TX: new balance  %% money_moved = true
    end
    end

    rect rgb(235,255,235)
    Note over TX,DB: STEP 8–9 — Record ledger + logs in ONE commit
    TX->>DB: transactions.create_transaction(...)
    TX->>DB: logs.log_to_database(debit-leg); logs.log_to_database(credit-leg)
    TX->>DB: uow.commit() (once)
    end

    TX->>N: send_notification(sender), send_notification(recipient)  %% best-effort
    TX->>DB: idempotency.complete(key, result); uow.commit()
    TX-->>GW: 200 {transaction_id, balances, ...}
    GW-->>U: 200
```

## Why it's built this way

- **Idempotency, insert-first.** The key is `reserve()`d (and committed) *before*
  any money moves. A completed key **replays** the stored result; a duplicate
  still in progress is rejected `409` — the transfer never executes twice. Once
  both legs settle (`money_moved`), a later failure must **not** release the key.
- **Gateway before money.** Payment-gateway authorization happens *before* the
  first debit, so a rejection has zero financial impact.
- **Compensation, not distributed transaction.** Debit and credit are two
  independent calls to `accounts_service`. If the **credit** fails after a
  successful **debit**, the service **credits the source back** (reversal). A
  failed reversal is logged `CRITICAL` for manual reconciliation.
- **One local commit.** The ledger row and both log legs are written through the
  same `TransactionUnitOfWork` and committed once — the local record is
  all-or-nothing even though the money move is a saga.
- **Notifications never block.** Both notifications are best-effort; a
  notification outage is logged and swallowed (the transfer already succeeded).

## Failure modes

| Failure point | Outcome |
|---|---|
| Validation / limits / PIN | 4xx; no money moved |
| Gateway rejects | 402; no money moved |
| Debit fails | error; no money moved |
| Credit fails (post-debit) | debit reversed; 500; funds preserved |
| Reversal fails | `CRITICAL` log; manual reconciliation |
| Ledger/log commit fails after money moved | error surfaced; key **not** released (retry-safe) |
| Notification fails | transfer still succeeds (200) |



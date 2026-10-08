# C4 Level 2 — Container

Each **container** is a separately deployable process (a ASP.NET Core Web API service or a
database). Arrows are synchronous HTTP/JSON calls unless noted.

```mermaid
flowchart TB
    fe["🖥️ Frontend (React)<br/>:3000"]
    gw["🚪 central_gateway<br/>API Gateway :8000"]

    subgraph core["Core banking (Gold tier — own DBs)"]
        acc["accounts_service :8001"]
        txn["transactions_service :8002"]
        usr["users_service :8003"]
        aut["auth_service :8004"]
    end

    subgraph stubs["External-system stubs (Lite tier — no DB)"]
        aad["aadhar_service :8005"]
        com["company_crv_service :8006"]
        ntf["notification_service :8007"]
        pay["central_payment_gateway :8008"]
    end

    subgraph infra["Infrastructure (Edge tier)"]
        reg["registry_service :8010<br/>(service discovery)"]
    end

    accdb[("gdb_accounts_db")]
    txndb[("gdb_transactions_db")]
    usrdb[("gdb_users_db")]
    autdb[("gdb_auth_db")]

    fe --> gw
    gw --> acc & txn & usr & aut & aad & com & ntf & pay

    aut --> usr
    acc --> aad & com & ntf
    txn --> acc & pay & ntf

    acc --> accdb
    txn --> txndb
    usr --> usrdb
    aut --> autdb

    core -. "register / heartbeat / resolve" .-> reg
    stubs -. "register / heartbeat" .-> reg
```

## Containers

| Container | Port | Responsibility | Data store |
|---|---|---|---|
| **central_gateway** | 8000 | Single entry point: routing/proxy, CORS, rate limiting | — |
| **accounts_service** | 8001 | Savings/current accounts, balances, debit/credit, KYC orchestration | `gdb_accounts_db` |
| **transactions_service** | 8002 | Deposits, withdrawals, transfers (saga), transaction logs, transfer limits | `gdb_transactions_db` |
| **users_service** | 8003 | User management (CRUD), roles, audit log | `gdb_users_db` |
| **auth_service** | 8004 | Login, JWT issuance/verification, token revocation, audit | `gdb_auth_db` |
| **aadhar_service** | 8005 | Aadhaar verification (UIDAI stub) | — |
| **company_crv_service** | 8006 | Company registration verification (MCA stub) | — |
| **notification_service** | 8007 | Notifications (stub) | — (in-memory) |
| **central_payment_gateway** | 8008 | Payment authorization (gateway stub) | — |
| **registry_service** | 8010 | Service discovery (register/heartbeat/resolve) | — (in-memory) |

## Key interactions

- **Frontend → Gateway → services** — all client traffic enters through the gateway.
- **auth → users** — auth verifies credentials against the users service (`/internal/v1/users/verify`).
- **accounts → aadhar/company/notification** — KYC verification on account opening + notifications.
- **transactions → accounts/payment/notification** — a transfer validates + debits/credits accounts, gets gateway approval, and notifies both parties (see the [fund-transfer sequence](../sequence-diagrams/fund-transfer.md)).
- **All services ↔ registry** — register on startup, heartbeat periodically, resolve peers.

## Cross-cutting (every container)

All services embed the same `gdb_common` middleware: correlation ID + W3C
`traceparent` propagation, structured JSON logs, Prometheus `/metrics`,
`/health` + `/live` + `/ready` probes, security headers, and a safe error
envelope. Inter-service calls carry `X-Internal-API-Key`; user calls carry a JWT.

Next: [Level 3 — Component](level-3-component.md).



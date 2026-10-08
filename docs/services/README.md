# Service Deep-Dives — Code-Level Flows

Layer-by-layer, **code-accurate** walkthroughs of the four gold services: the exact
route → DTO → use case → domain → mapping → repository → Unit of Work → database
path, with Mermaid diagrams, business rules, exception points, and the tables written.

These are the "how does it *actually* work" companions to the higher-level
[architecture sequence diagrams](../architecture/sequence-diagrams/README.md).

| Service | Port | Owns | Deep-dive |
|---|---|---|---|
| **accounts_service** | 8001 | Savings/current accounts, balances, KYC | [accounts-service-flow.md](accounts-service-flow.md) |
| **transactions_service** | 8002 | Deposits, withdrawals, transfers (saga), limits | [transactions-service-flow.md](transactions-service-flow.md) |
| **users_service** | 8003 | Users, roles, audit | [users-service-flow.md](users-service-flow.md) |
| **auth_service** | 8004 | Login, JWT, token revocation | [auth-service-flow.md](auth-service-flow.md) |

## How to read these

Every service follows the same clean-architecture shape (the dependency rule points
inward). Each doc has the same sections:

1. **Overview** — what the service owns, its port and database.
2. **Layered architecture** — a flowchart of the layers and the dependency direction.
3. **Primary flow** — a code-level sequence diagram of the main write path (exact files/functions).
4. **Layer-by-layer walkthrough** — the file and function at each step.
5. **Endpoint map** — the service's routes.
6. **Business rules** — the invariants enforced.
7. **Exception points** — what can fail and the resulting HTTP status.
8. **Database** — the tables and what is written.

> **Diagrams** are Mermaid, so they render on GitHub and most IDEs.
> **Accuracy:** written against the current code — file paths are clickable and
> function/class names are exact.



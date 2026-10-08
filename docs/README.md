# GDB Platform — Documentation

Documentation for the **Global Digital Bank (GDB)** microservices platform: a
ASP.NET Core Web API + Entity Framework Core platform built on Clean / Hexagonal Architecture with a
shared `gdb_common` library.

> **Reference implementation:** `accounts_service` is the gold-standard template.
> Every stateful service follows its structure exactly.

## Start here

| I want to… | Read |
|---|---|
| Understand the whole system | [architecture/system-architecture.md](architecture/system-architecture.md) |
| See the C4 diagrams | [architecture/c4/](architecture/c4/README.md) |
| Trace a key flow (transfer, login, …) | [architecture/sequence-diagrams/](architecture/sequence-diagrams/README.md) |
| Trace a service end-to-end at the code level | [services/](services/README.md) — accounts · transactions · users · auth |
| Know *why* a decision was made | [architecture/adr/](architecture/adr/README.md) |
| Run a service locally | [development/running-locally.md](development/running-locally.md) |
| Switch the database | [development/switching-databases.md](development/switching-databases.md) |
| Add a new endpoint / use case | [development/adding-a-use-case.md](development/adding-a-use-case.md) |
| Write / run tests | [development/testing.md](development/testing.md) |
| Follow the coding standard | [development/coding-standards.md](development/coding-standards.md) |
| Operate the platform (health, metrics, logs) | [operations/](operations/README.md) |
| Troubleshoot a problem | [operations/troubleshooting.md](operations/troubleshooting.md) |

## Documentation map

- **architecture/** — how the system is built.
  - `system-architecture.md` — the current high-level architecture and service tiers.
  - `enterprise-blueprint.md`, `enterprise-microservice-standard.md` — the enterprise target.
  - `request-flow.md`, `service-development-guide.md`, `developer-checklist.md`.
  - `provider-strategy.md`, `repository-strategy.md`, `mapping-strategy.md`, `migration-template.md`.
  - **c4/** — C4 model (System Context → Container → Component).
  - **sequence-diagrams/** — key runtime flows as Mermaid sequence diagrams.
  - **adr/** — Architecture Decision Records (frozen, append-only).
- **services/** — code-level deep-dives per gold service (accounts, transactions, users, auth): route → domain → repository → DB, with Mermaid flow/sequence/ER diagrams.
- **development/** — how to build on the platform (run, switch DB, add a use case, test, coding standards).
- **operations/** — how to run it in production (health, metrics, tracing, logs, troubleshooting).
- **security/** — CORS, exception policy, OpenAPI compatibility, secret management.
- **enterprise-migration/** — Phase-0 baseline reports and inventories.

## Platform at a glance

| Tier | Services | Standard |
|---|---|---|
| **Gold** (full clean architecture) | accounts, transactions, users, auth | domain + Unit of Work + repository interfaces + composition root + async-generator DI |
| **Lite** (stateless stubs) | aadhar, company_crv, central_payment_gateway, notification | `api` + `dto` + thin services (no DB → no UoW/repositories) |
| **Edge** (infrastructure) | central_gateway (API gateway), registry (service discovery) | thin wrappers over shared `gdb_common` primitives |

**Cross-cutting (all services, via `gdb_common`):** correlation IDs, structured JSON logs,
W3C distributed tracing, Prometheus `/metrics`, safe exception envelope, security headers,
health/liveness/readiness probes, circuit breaker, rate limiting, JWT/RBAC, service discovery.

## Quality gates

| Gate | What it enforces | How to run |
|---|---|---|
| Test suites | Per-service unit/API/integration tests | `cd <service> && DATABASE_PROVIDER=inmemory ./dotnet test gdb-service-dotnet.slnx` |
| Architecture gate | Dependency rule (domain has no frameworks; services don't import infrastructure) | `dotnet test gdb-service-dotnet.slnx libs/gdb_common/tests/test_architecture.py` |
| OpenAPI gate | API contracts unchanged vs. frozen snapshots | `dotnet run --project tools/Gdb.OpenApiGate` |



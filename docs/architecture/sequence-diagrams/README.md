# Sequence Diagrams

Runtime flows for the platform's most important operations, as Mermaid sequence
diagrams. They show the **implemented** call sequence — the participants are real
services, ports, and the Unit of Work.

| Flow | Diagram | Services involved |
|---|---|---|
| Authenticate (login) | [authentication.md](authentication.md) | auth → users |
| Open an account (with KYC) | [account-creation.md](account-creation.md) | accounts → aadhar/company → notification |
| Transfer funds | [fund-transfer.md](fund-transfer.md) | transactions → accounts → payment gateway → notification |
| Cross-cutting: one request end-to-end | [request-observability.md](request-observability.md) | gateway → any service (correlation/trace/metrics) |

Conventions:

- **UoW** = the service's session-owning Unit of Work; it commits **once** per use case.
- Inter-service calls carry `X-Internal-API-Key` + `X-Correlation-ID` + `traceparent`.
- End-user calls carry a `Bearer` JWT and enter through the **gateway**.



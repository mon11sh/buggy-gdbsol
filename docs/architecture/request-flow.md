# Request & Response Flow (Mandatory)

Every request in every GDB microservice follows this flow **exactly**. See the master [enterprise-microservice-standard](enterprise-microservice-standard.md).

## Request flow (sequence)
```
Client
  → API Router                     app/api/routers/*.py         (@router.*, Depends auth/RBAC/internal-key)
  → Request DTO                    app/application/dto/*.py      (C# DTOs — shape/format validation)
  → Request Validator              app/application/validators/   (cross-field/business-format checks)
  → DTO → Domain Mapper            app/infrastructure/mappers/   (DtoMapper.to_domain — EXPLICIT)
  → Application Use Case           app/application/use_cases/    (orchestration ONLY)
  → Domain Model                   app/domain/                   (BUSINESS LOGIC ONLY here)
  → Repository Interface           app/domain/repositories/      (domain-termed methods)
  → Unit of Work                   IUnitOfWork (impl in infra)   (begin → work → commit/rollback)
  → Repository Implementation      app/infrastructure/persistence/repositories/
  → Domain → ORM Mapper            app/infrastructure/mappers/   (Mapper.to_record — EXPLICIT)
  → ORM Models                     app/infrastructure/persistence/orm/
  → Database Provider              app/infrastructure/persistence/providers/  (selected in Composition Root)
  → Database
```

## Response flow (sequence)
```
Database
  → ORM Model                      infrastructure/persistence/orm/
  → Repository Implementation      reads within the UoW session
  → Domain Model                   Mapper.to_domain (ORM → Domain — EXPLICIT)
  → Response Mapper                DtoMapper.to_response (Domain → DTO — EXPLICIT; masking, currency, etc.)
  → Response DTO                   C# DTOs (frozen contract)
  → API Layer                      response_model + status_code
  → Client                         (+ X-Correlation-ID, security headers, safe error envelope via gdb_common)
```

## Rules
- The API layer never sees a Domain model or an ORM row — only DTOs.
- The Application layer never sees an ORM row, a Session, or an HTTP client — only Domain objects, repository interfaces, ports, and DTOs.
- The Domain layer never imports a framework and never performs I/O.
- Errors propagate as typed **domain/application exceptions**; the API/`gdb_common` exception layer maps them to the standard envelope `{error_code, message, status, correlation_id, request_id}` — never `str(exc)` or a stack trace (Phase 1).
- Validation is layered: **format** at the DTO/validator edge; **invariants** in the Domain; DB constraints are a last-resort safety net, not the primary rule location.



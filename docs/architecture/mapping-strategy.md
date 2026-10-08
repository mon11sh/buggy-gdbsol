# Mapping Strategy (Mandatory)

Implements ADR-005 on top of `gdb_common.application.Mapper` / `DtoMapper`. **Explicit mapping only — reflection-based and implicit mapping are prohibited.**

## Two explicit mapping layers
```
Inbound:   Request DTO ──(DtoMapper.to_domain)──►  Domain Model
Persist:   Domain Model ──(Mapper.to_record)────►  ORM Model  ──► Database
Load:      ORM Model    ──(Mapper.to_domain)────►  Domain Model
Outbound:  Domain Model ──(DtoMapper.to_response)► Response DTO
```

## Rules
1. **Two mapper families per aggregate**, both in `app/infrastructure/mappers/`:
   - `DtoMapper[TDomain, TDtoIn, TDtoOut]` — `to_domain(dto)` and `to_response(entity)` (DTO ↔ Domain).
   - `Mapper[TDomain, TRecord]` — `to_domain(record)` and `to_record(entity)` (ORM ↔ Domain).
2. **ORM models NEVER cross a boundary.** They exist only inside `infrastructure/persistence`. Application/API see Domain objects and DTOs only.
3. Mapping methods are **hand-written and explicit** — every field is mapped by name in code. No `automapper`, no `from_orm` blanket copies, no `**dict` field spraying across layers.
4. Sensitive-field handling lives in the mapper (infrastructure): encrypt/decrypt PII, compute blind indexes, mask fields for responses — preserving the frozen contract (e.g. Aadhaar `********NNNN`). The Domain holds plaintext value objects; the mapper encrypts on write and decrypts on read.
5. DTO validation is **format** validation (C# DTOs); business invariants are enforced in the Domain, not the mapper.

## Why explicit
- Decouples the frozen API DTO (ADR-009) from the DB schema — either can evolve without rippling into the other.
- Prevents accidental over-exposure of internal/sensitive fields (`pin_hash`, ciphertext, revocation flags).
- Keeps the Domain free of C# DTOs/ORM base classes.
- Field-by-field intent is explicit and unit-testable.

## Testing
- **Mapper Tests**: round-trip `Domain → ORM → Domain` equality; `Domain → Response DTO` matches the frozen contract (including masking); `Request DTO → Domain` produces valid value objects.



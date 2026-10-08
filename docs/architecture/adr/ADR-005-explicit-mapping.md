# ADR-005 — Explicit Mapping (never expose ORM models)

- **Status:** Accepted
- **Related:** ADR-001, ADR-002, ADR-003, blueprint §8–11

## Context
Phase 0 confirmed **no dedicated Mapper exists**: conversion is ad-hoc — manual dict-building in repositories (`_transfer_to_dict`, `_log_to_dict`, `account_to_dict`) plus C# DTOs `from_attributes`. ORM shapes and DTOs leak into each other, and DTOs double as the domain. This blocks a clean domain and couples the API contract to the persistence schema.

## Decision
Use **explicit mapping** at every boundary. Data is always translated through dedicated mappers:

```
DTO  ──(inbound)──▶  Domain Entity  ──(persist)──▶  ORM row
DTO  ◀─(outbound)──  Domain Entity  ◀──(load)────   ORM row
```

- **ORM models never leave Infrastructure.** They are not returned to Application or API, and are not serialized to clients (fixes the current leakage).
- Each aggregate has a **`<Aggregate>Mapper`** in `infrastructure/mappers/` with explicit methods: `to_entity(orm) -> Entity`, `to_orm(entity) -> ORM`, `to_response_dto(entity) -> Response`, and `from_request_dto(dto) -> domain input`.
- Mapping is **hand-written and explicit** — no reflection-magic that silently couples fields.

### Why explicit mapping is preferred
1. **Decoupling / contract stability:** the API DTO (frozen — ADR-009) and the DB schema can evolve independently; a column rename doesn't ripple into the response shape.
2. **No accidental over-exposure:** automatic ORM→JSON (e.g. returning ORM objects or blanket `from_attributes`) risks leaking internal fields (`pin_hash`, encrypted `aadhar_number`, `aadhar_hash`, revoked-token internals). Explicit mappers expose exactly the frozen response fields — and preserve the **Aadhaar masking `********NNNN`** and decrypt-on-read contract (R-10).
3. **Domain purity:** entities stay free of C# DTOs/ORM base classes (ADR-001); the mapper is the only place that knows all three shapes.
4. **Testability & clarity:** mapping is unit-testable in isolation; field-by-field intent is explicit, not implicit.
5. **Provider independence:** different providers can have different ORM/SQL shapes; only the mapper differs, callers don't (ADR-003/006).

## Consequences
**Positive:** stable contracts, no field leakage, pure domain, safe handling of encrypted/masked fields.
**Negative:** boilerplate — one mapper per aggregate, updated when fields change (mitigated: mappers are trivial and well-tested).
**Δ from today:** replace ad-hoc `_*_to_dict` helpers + `from_attributes` with explicit mappers, incrementally per slice.

## Alternatives considered
- **Return ORM models / blanket `from_attributes`:** rejected — leaks internal + sensitive fields, couples contract to schema.
- **DTOs as domain (status quo):** rejected — anemic, framework-coupled, no invariants (see ADR-002).



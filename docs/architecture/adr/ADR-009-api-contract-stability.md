# ADR-009 — API Contract Stability Throughout the Migration

- **Status:** Accepted
- **Related:** ADR-005, ADR-008, blueprint §7–9, §31, api-route-inventory, backward-compatibility-contracts

## Context
The migration re-homes logic into Clean Architecture layers *behind* the existing APIs. Phase 0 catalogued ~60 routes, their DTOs, status codes, error envelopes, headers, provider names, table/column names, ports, and inter-service URLs (see `api-route-inventory.md`, `backward-compatibility-contracts.md`). The React frontend and every inter-service client depend on these exact shapes. A refactor that "tidies" a path or model would break consumers (R-09).

## Decision
**All external and inter-service contracts remain backward compatible for the entire migration.** Refactoring is internal-only; the observable surface does not change unless via an explicit, versioned, additive decision.

### What is frozen (must not change silently)
- **Route paths & methods**, including the known inconsistencies preserved as-is: auth's `openapi_url="/openapi.json"` + `/redoc`; users' internal prefix `/internal/v1` vs accounts' `/api/v1/internal`.
- **Request DTO fields** — names, types, validation (e.g. `login_id` regex, `initial_balance>=2000`, `pin` length).
- **Response DTO fields** — names, types, including **Aadhaar masking `********NNNN`** and `BalanceResponse.currency="INR"`.
- **HTTP status codes** — e.g. account/user/transaction creates return **201**; verify-pin lockout **423**; login throttle **429**.
- **Error envelope shapes** — the per-service `{error_code,message,...}` / `{error,message}` forms currently returned (target: converge on one shape **additively**, never by removing fields consumers read).
- **Auth headers** — `Authorization: Bearer`, `X-Internal-API-Key`, `X-Correlation-ID`, `Idempotency-Key` (exact names).
- **Provider names** — the five `DATABASE_PROVIDER` values.
- **DB identifiers** — table/column/index/unique/FK names (co-frozen with ADR-008).
- **Service ports** (8000–8010) and **inter-service URL env vars**, including the dual plural/singular aliases (`ACCOUNTS_SERVICE_URL`/`ACCOUNT_SERVICE_URL`, and `DATABASE_*` vs `DB_*`).

### How compatibility is guaranteed
1. **OpenAPI snapshot tests** per service: the generated schema must be unchanged except for intended additions (blueprint §32). This is the automated gate.
2. **Additive-only change rule:** new optional fields/endpoints are allowed; renames/removals/type-narrowing of existing fields are **not**, except behind a new API version.
3. **Versioning path:** if a breaking change is ever required, introduce it under a **new version** (e.g. `/api/v2/...`) and keep `/api/v1` until all consumers migrate — never mutate v1 in place.
4. **Contract review** accompanies any PR that touches `api/`, `models/`, routers, ORM identifiers, or settings env names.
5. **Explicit mapping (ADR-005)** decouples the frozen DTOs from internal domain/ORM change, making internal refactors invisible to clients.

## Consequences
**Positive:** consumers (frontend, inter-service) keep working through every phase; refactors are safe; regressions are caught by snapshot gates.
**Negative:** some Phase-0 inconsistencies must be carried unchanged (documented as known contracts, not bugs to "fix" mid-migration); genuine contract improvements wait for a v2.

## Alternatives considered
- **Refactor contracts opportunistically during migration:** rejected — guarantees consumer breakage (R-09), conflates internal and external change.
- **Freeze code entirely:** rejected — the goal is to change internals safely, not to stop improving; stability is on the *contract*, not the implementation.



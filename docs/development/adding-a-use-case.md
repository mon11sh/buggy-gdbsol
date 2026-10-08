# Adding a Use Case (or Endpoint)

A practical, layer-by-layer walkthrough for adding a new operation to a **gold**
service, following the `accounts_service` reference. It complements the
[coding standards](coding-standards.md), [service development guide](../architecture/service-development-guide.md),
and [developer checklist](../architecture/developer-checklist.md).

> **The golden rule (enforced):** dependencies point **inward**. Add code in the
> layer that owns the concern, and never let `domain/` import a framework or
> `services/` import `app.infrastructure`. The architecture gate
> (`libs/gdb_common/tests/test_architecture.py`) will fail the build otherwise.

## Where each piece goes

```
api/         →  the HTTP endpoint (thin: auth, (de)serialize, call the use case)
dto/         →  request/response schemas (the wire contract)
services/    →  the use case (orchestrate: load via UoW, apply rules, persist, commit once)
domain/      →  new rules (rules.py), models/value objects, or ports (ports.py)
mapping/     →  DTO ↔ domain ↔ ORM conversions
repositories/interfaces/  →  a new method on the repository contract (if persistence needs it)
infrastructure/persistence/repositories/  →  implement it in BOTH Entity Framework Core + InMemory
integration/ →  a new outbound HTTP client (only if you call another service)
```

## Step by step

Adding, say, "close an account":

1. **Domain first.** Put the rule in `domain/rules.py` (e.g. "an account with a
   non-zero balance cannot be closed") and any new state on the `Account`
   aggregate/enum. pure C# — no ASP.NET Core Web API/Entity Framework Core/C# DTOs here.

2. **Port (only if you call outside).** If the use case needs an external system,
   declare an abstract port in `domain/ports.py`; implement it as an HTTP client
   in `integration/` and wire it in the composition root. Use cases depend on the
   **port**, never the client.

3. **Repository contract (only if persistence changes).** Add the method to the
   interface in `repositories/interfaces/`, then implement it in **both**
   `infrastructure/persistence/repositories/` classes (Entity Framework Core **and**
   InMemory) so every provider and the tests stay consistent.

4. **Use case.** Add a thin method/class in `services/`. It:
   - takes the `UnitOfWork` + any ports via constructor injection,
   - loads aggregates through `uow.<repo>`,
   - calls `domain.rules` for the business decision,
   - writes through the repositories,
   - calls `uow.commit()` **exactly once**.
   No business rules inline; no `app.infrastructure` import.

5. **DTOs.** Add request/response schemas in `dto/` (C# DTOs). This is the frozen
   wire contract — see [ADR-009](../architecture/adr/ADR-009-api-contract-stability.md).

6. **Mapping.** Add explicit conversions in `mapping/` (DTO→domain, domain→ORM,
   ORM→domain). Never expose ORM models over the API ([ADR-005](../architecture/adr/ADR-005-explicit-mapping.md)).

7. **Endpoint.** Add the route in `api/`. Keep it thin: auth/RBAC dependency,
   deserialize the DTO, resolve the use case via `dependencies/` (async-generator
   DI), call it, serialize the response. **Keep docstrings** — ASP.NET Core Web API uses them
   as the OpenAPI `description`.

8. **Wire DI.** If you introduced a new use case class, add its provider in
   `dependencies/` (build per request; the async generator `aclose()`s the UoW in
   `finally`) and wire construction in `composition/`.

## Validate (all must stay green)

```powershell
# 1. Tests for the service (unit + API + integration)
cd accounts_service; $env:DATABASE_PROVIDER="inmemory"; ./dotnet test gdb-service-dotnet.slnx

# 2. Architecture gate (dependency rule)
cd ..; libs/gdb_common/dotnet test gdb-service-dotnet.slnx libs/gdb_common/tests/test_architecture.py

# 3. OpenAPI gate — a NEW endpoint is a contract change, so refresh the snapshot intentionally
dotnet run --project tools/Gdb.OpenApiGate accounts_service          # see the diff first
dotnet run --project tools/Gdb.OpenApiGate --update accounts_service # accept it (additive only)
```

> The OpenAPI gate **should** flag a new endpoint — that's the point. Review the
> diff, confirm it is **additive** (new optional fields / new routes, never
> renames/removals of existing ones), then `--update` the snapshot. See
> [testing.md](testing.md).

## Checklist

- [ ] Rule lives in `domain/rules.py`, not the service body
- [ ] `domain/` imports no framework; `services/` imports no `app.infrastructure`
- [ ] Repository method added to the interface **and both** implementations
- [ ] `uow.commit()` called exactly once
- [ ] DTOs added; mapping is explicit; ORM never leaves infrastructure
- [ ] Route is thin; docstring preserved; RBAC dependency applied
- [ ] Tests + architecture gate green; OpenAPI diff reviewed and (if additive) snapshot updated



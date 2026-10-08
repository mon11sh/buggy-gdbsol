# GDB Coding Standards

Applies to all Python in `gdb-service`. Complements the [enterprise-blueprint](../architecture/enterprise-blueprint.md) (§30–32) and the [ADRs](../architecture/adr/). These standards are enforced by review and by the Phase-2 **architecture tests** (`libs/gdb_common/tests/test_architecture.py`).

## Typing
- **Full type hints on every public signature** (params + return). No bare `Any` in `domain/` or `application/`.
- Use `from __future__ import annotations` in new modules.
- Generics via `typing.TypeVar` / `Generic` (see `gdb_common.domain.Entity`, `gdb_common.application.Repository`).
- Value objects are typed **immutable** `@dataclass(frozen=True)`.
- ORM uses Entity Framework Core 2.0 `Mapped[...]` (infrastructure only).

## Docstrings
- **Google-style** docstrings on every public module, class, and function: one-line summary, then `Args:` / `Returns:` / `Raises:` where applicable (matches `gdb_common`).
- Document **invariants** a domain method enforces and **error codes** an exception carries.
- No secrets, PINs, full Aadhaar, or tokens in docstrings or examples.

## Imports
- Order: stdlib → third-party → local; no wildcard imports.
- **Dependency-rule imports (hard):**
  - `domain/` imports **nothing** external — no ASP.NET Core Web API, Entity Framework Core, C# DTOs, httpx, settings, logging frameworks.
  - `application/` imports domain + application contracts only — **no** infrastructure, **no** frameworks, **no** composition.
  - `infrastructure/` may import domain (to implement its interfaces), drivers, `gdb_common`.
  - Only the **composition root** (`dependencies/providers.py`, `main.py`) imports concretes from every layer.
- Keep heavy imports (Entity Framework Core) lazy inside methods where it keeps module import cheap (existing provider pattern).

## Folder structure
- Per-service target layers (additive; no folder moves this phase): `api/` → `application/` → `domain/` → `infrastructure/`, with `models/` (DTOs), `dependencies/` (composition root), `config/`. See blueprint §4.
- Shared foundation lives in `libs/gdb_common/gdb_common/{domain,application,composition}`.
- New code goes in the correct layer; do not add business logic to `gdb_common`.

## Dependency rules (SOLID / Clean Architecture)
- **Constructor injection only.** No service locator; no global singleton resolution inside business logic; no repository/provider creation inside services or routes (ADR-007).
- Depend on **interfaces** (repository/UoW/mapper contracts), not concretes.
- Repositories return/accept **domain objects** — never ORM rows, Sessions, or dicts (ADR-003).
- Provider selection lives only in Infrastructure/Composition (`ProviderRegistry`) — ADR-006.

## Async conventions
- `async def` end-to-end (routes → application → repositories → integration).
- Never block the event loop: no `requests`, no `time.sleep`, no sync DB in async paths; offload CPU-bound work (e.g. bcrypt) to a thread pool.
- One session/connection per unit of work; repositories never open/commit their own transaction (ADR-004).

## Logging
- Use stdlib `logging` via `gdb_common` observability (structured JSON with correlation id).
- Always within the correlation context; **never** log secrets/PINs/full Aadhaar/tokens.
- Errors: raise typed exceptions; the shared handler (`gdb_common.install_exception_handlers`) returns a safe envelope — never return `str(exc)` or a stack trace to clients (Phase 1).

## Testing
- **Test pyramid:** many domain unit tests (pure) → application tests with an **InMemory** unit of work/repositories → fewer API tests (TestClient) → contract/snapshot tests → optional cross-provider tests.
- Every service is **independently testable** in its own venv (`dotnet test gdb-service-dotnet.slnx`, `dotnet test gdb-service-dotnet.slnx-asyncio`, `dotnet test gdb-service-dotnet.slnx-cov` — see `requirements-dev.txt`).
- **Backward-compat gate:** the OpenAPI snapshot (`scripts/run_openapi_gate.py check`) must pass; changes are additive-only (ADR-009).
- **Architecture gate:** `libs/gdb_common/tests/test_architecture.py` must pass (dependency rules).
- Do not modify existing tests to make new code pass; add new tests.

## Naming
- Modules `snake_case`; classes `PascalCase`; entities/value-objects are nouns without suffixes (`Account`, `Money`); repository interface `<Aggregate>Repository`, impl `<Provider><Aggregate>Repository`; DTOs `<Name>Request`/`<Name>Response`; ORM `<Name>ORM`; DI providers `get_<thing>`; domain exceptions `<Reason>Error`; env vars `UPPER_SNAKE_CASE`.
- **Frozen public names** (routes, DTO fields, table/column names, env vars) keep their current spelling regardless — ADR-009.



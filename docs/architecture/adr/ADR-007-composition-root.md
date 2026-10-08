# ADR-007 — Composition Root & Dependency Injection

- **Status:** Accepted
- **Related:** ADR-001, ADR-003, ADR-004, ADR-006, blueprint §14–16, migration-risk-register R-08

## Context
Phase 0 confirmed DI is done via ASP.NET Core Web API `Depends` + `dependencies/providers.py` factories + a process-wide `get_provider()` singleton — but with leaks: `AccountService.__init__` **self-resolves** `get_provider()` (a Dependency-Inversion violation), and transactions keeps **module-level mutable service singletons** (`deposit_service = DepositService()`) in the request path (R-08). There is no single, explicit composition root.

## Decision
Adopt an explicit **Composition Root** as the one place that constructs concretes and injects them; use **constructor injection** everywhere else.

### Dependency Injection
- **Constructor injection only.** Application services, repositories, UoW, mappers, and integration clients receive their collaborators as constructor arguments. **No class resolves its own dependencies** (fixes the `AccountService` leak).
- Domain and Application depend on **interfaces**; concretes are supplied from outside.

### Composition Root
- **Location:** `dependencies/providers.py` (per service) + `main.py` app assembly. This is the **only** code allowed to import concretes from every layer.
- **Responsibility:** given a request, build the object graph — `provider = get_provider()` → `uow = provider.unit_of_work()` (per request) → `mapper`, `integration clients` → `ApplicationService(uow=..., mapper=..., ...)` → hand to ASP.NET Core Web API `Depends`.
- **Lifetimes:** the provider/engine is a **process singleton**; the UoW and application services are **per-request**; there are **no mutable service singletons on the request path** (Δ — remove today's module-level `*_service` globals).

### Provider Factory
- The composition root gets its persistence concretes exclusively from the **Provider Factory** (ADR-006) — the single switch on `DATABASE_PROVIDER`. The root never hard-codes a provider.

### Application Wiring
- `main.py` installs cross-cutting edges (`install_observability`, CORS, exception handlers, routers) and mounts routes whose handlers depend on the composition-root factories.
- Wiring is **declarative and centralized**: to change an implementation (e.g. add a provider, swap a mapper, inject a fake for a test), edit only the composition root — never Domain/Application/API.

## Consequences
**Positive:** testable (inject InMemory UoW/fakes), no hidden global state, one obvious place to understand wiring; fixes R-08 and the DI leak.
**Negative:** more explicit wiring code; care needed to keep per-request vs singleton lifetimes correct.
**No new DI library is introduced** — ASP.NET Core Web API `Depends` + factory functions remain the mechanism (a container library is explicitly out of scope for this architecture freeze).

## Alternatives considered
- **Self-resolving services / global singletons (status quo):** rejected — hidden coupling, hard to test, incorrect across replicas (R-08).
- **Introduce a DI-container library (e.g. dependency-injector):** rejected for now — adds a dependency; ASP.NET Core Dependency Injection + a disciplined composition root suffice.



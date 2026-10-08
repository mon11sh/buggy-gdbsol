# Provider Strategy (Mandatory)

Implements ADR-006 on top of `gdb_common.composition.ProviderRegistry`.

## Rules
1. **Provider selection occurs ONLY in the Composition Root.** The Application/Domain/API layers never know which provider is active.
2. Supported providers (the frozen `DATABASE_PROVIDER` values): `inmemory`, `sqlite`, `mysql`, `postgres`, `supabase`.
3. Each service's Composition Root registers a factory per provider in a `ProviderRegistry` and resolves one by `settings.DATABASE_PROVIDER`.
4. **pgAdmin is NOT a provider** — it is a PostgreSQL administration GUI. There is no `pgadmin` value and no factory branch. `ProviderRegistry.register("pgadmin", ...)` raises `UnknownProviderError`.
5. A provider yields: an engine/pool + session factory (SQL) or an in-memory store, and a **Unit of Work factory** wired to that provider's repository implementations.

## Adding a future provider (MongoDB, Azure SQL, Oracle, CockroachDB)
Only TWO things change — **nothing** in Application, Domain, DTOs, API, or business logic:
```
1. Create a new Repository Implementation (+ Mapper if the storage shape differs)   [infrastructure]
2. Register its factory in the Composition Root's ProviderRegistry                   [composition]
```
Note: adding a value OUTSIDE the five standard names also requires extending `SUPPORTED_PROVIDERS` in `gdb_common.composition.provider_registry` (a deliberate, reviewed change), since the registry validates names.

## Resolution flow
```
DATABASE_PROVIDER → Settings → CompositionRoot → ProviderRegistry.create(name)
    → DatabaseProvider → engine/pool + session factory → UnitOfWork(+ repositories)
    → injected into Use Cases via Depends
```

## Verification requirement
"Implemented" ≠ "verified working". A provider is production-ready only after the service's repository-contract + integration tests pass against a **live** instance of that engine (today only inmemory + accounts-sqlite are verified — see provider-support-matrix).



# Repository Strategy (Mandatory)

Implements ADR-003 on top of the Phase-2 `gdb_common.application.Repository` contract.

## Rules
1. **One repository interface per aggregate**, owned by the Domain (`app/domain/repositories/`). It extends `gdb_common.application.Repository[TAggregate, TId]`.
2. Interfaces expose **only business methods** in domain terms, e.g.:
   `create()/add()`, `update()`, `delete()`, `find_by_id()/get()`, `find_all()`, `find_by_business_key()` (e.g. `exists_with_aadhaar`, `find_by_login_id`).
3. Interfaces MUST NOT expose: Entity Framework Core types, database Sessions/connections, ORM models, dicts, or any provider-specific object.
4. Repositories **return and accept Domain aggregates/entities** — never ORM rows or dicts.
5. Concrete implementations live in `app/infrastructure/persistence/repositories/`, one per provider, all satisfying the same interface:
   `InMemoryRepository`, `SQLiteRepository`, `MySQLRepository`, `PostgreSQLRepository`, `SupabaseRepository` (SQL variants typically share one Entity Framework Core implementation parameterised by the connection URL; a raw-driver variant is allowed where justified).
6. Implementations receive their **session/connection from the Unit of Work** and **never commit on their own** (the UoW owns the transaction — ADR-004).
7. Implementations use the **Mapper** to convert ORM ↔ Domain at the boundary (ADR-005).

## Contract vs implementation
```
app/domain/repositories/account_repository.py        # interface (business methods, domain types)
app/infrastructure/persistence/repositories/
    Entity Framework Core_account_repository.py                  # SQLite/MySQL/Postgres/Supabase
    inmemory_account_repository.py                    # tests / inmemory provider
```

## Testing
- **Repository Contract Tests**: run the SAME test suite against EVERY implementation (in-memory + at least sqlite) to prove interchangeability.
- **Repository Implementation Tests**: provider-specific edge cases (unique constraints, blind-index lookups, etc.).
- Never assert on ORM internals from application/domain tests.



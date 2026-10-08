# Switching Databases

The platform supports five persistence providers. Switching is **configuration
only** — no code changes — because every stateful service selects its provider at
the composition root and all SQL dialects share **one** Entity Framework Core implementation.
See [ADR-006](../architecture/adr/ADR-006-provider-strategy.md) and
[provider-strategy.md](../architecture/provider-strategy.md).

## The five providers

| `DATABASE_PROVIDER` | Server needed? | Driver | Notes |
|---|---|---|---|
| `inmemory` | no | — | pure RAM; tests + fastest dev. No persistence. |
| `sqlite` | no | `aiosqlite` | each service gets its own `.db` file. |
| `mysql` | yes | `aiomysql` | set `DATABASE_URL`. |
| `postgres` | yes | `asyncpg` | local PostgreSQL / pgAdmin. |
| `supabase` | yes | `asyncpg` | hosted PostgreSQL (Supabase). |

> **pgAdmin is not a provider** — it's just a GUI for a PostgreSQL server; use
> `postgres`. (ADR-006.)

## How selection works

Each service reads two settings (12-factor env):

```
DATABASE_PROVIDER = inmemory | sqlite | mysql | postgres | supabase
DATABASE_URL      = <async Entity Framework Core URL>   # ignored for inmemory/sqlite
```

- The provider **factory** builds the right provider; the Entity Framework Core provider
  serves sqlite/mysql/postgres/supabase, the in-memory provider serves the rest.
- The provider hands out the session-owning **Unit of Work**; nothing above
  infrastructure knows which database is behind it.

## Switch every service at once

```powershell
# no-server providers (writes .env for all DB services, clears DATABASE_URL):
dotnet run --project tools/Gdb.Setup sqlite
dotnet run --project tools/Gdb.Setup inmemory

# server providers (write the connection settings):
dotnet run --project tools/Gdb.Setup <password> <user>   # local PostgreSQL / pgAdmin
dotnet run --project tools/Gdb.Setup                   # MySQL
python setup_supabase.py                # Supabase
```

Then restart: `dotnet run --project tools/Gdb.Runner`. On startup each DB service **auto-creates** its
database, tables, and seed data (dev convenience; EF Core Migrations is authoritative in
production — [ADR-008](../architecture/adr/ADR-008-database-migration-strategy.md)).

## Switch one service manually

Edit that service's `.env`:

```dotenv
DATABASE_PROVIDER=postgres
DATABASE_URL=postgresql+asyncpg://user:password@localhost:5432/gdb_accounts_db
```

Each stateful service owns its **own** database (`gdb_accounts_db`,
`gdb_transactions_db`, `gdb_users_db`, `gdb_auth_db`) — database-per-service, no
shared schema, no cross-service SQL.

## Run tests against a provider

Tests default to `inmemory` for speed and isolation:

```powershell
cd accounts_service
$env:DATABASE_PROVIDER="inmemory"; ./dotnet test gdb-service-dotnet.slnx
```

## Support status

All five providers are **fully supported** and switchable by config. `inmemory`
and `sqlite` need no server; `mysql`/`postgres`/`supabase` need a reachable server
and the driver (installed via `Gdb.Setup`).

## Related

- [ADR-006 provider strategy](../architecture/adr/ADR-006-provider-strategy.md) · [provider-strategy.md](../architecture/provider-strategy.md)
- [running-locally.md](running-locally.md) · [testing.md](testing.md)



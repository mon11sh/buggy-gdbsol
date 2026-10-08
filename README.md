# Global Digital Bank (GDB) — Microservices Platform

> **Reviewing the code?** Start with [CONCEPTS_NAVIGATOR.md](CONCEPTS_NAVIGATOR.md) — every concept from `if/else` to SOLID, patterns and enterprise hardening, pinned to file and line.

A training-grade full-stack banking application: **10 ASP.NET Core Web API microservices** + a
**React (Vite)** frontend, runnable against **six databases** (In-Memory, SQLite,
PostgreSQL, MySQL, SQL Server, Supabase) with **zero code changes** via a provider/factory
abstraction.

> **Login:** `admin` / `Welcome@1`  (also `teller` / `manager`)

## Services & ports
| Service | Port | Role |
|---|---|---|
| accounts | 8001 | account lifecycle, balances, PIN |
| transactions | 8002 | transfers, deposits, limits, idempotency |
| users | 8003 | platform users, RBAC |
| auth | 8004 | login, JWT issuance |
| aadhar (mock) | 8005 | KYC verification stub |
| company (mock) | 8006 | company registration stub |
| notification (mock) | 8007 | notifications stub |
| payment gateway (mock) | 8008 | payment rail stub |
| registry | 8010 | service discovery |
| gateway | 8000 | central API gateway |
| frontend | 3000 | React web app |

## Setup from scratch

### Prerequisites
- **.NET 10 SDK** — `dotnet --version` should report `10.x`
- **Node.js & npm** — `node --version` and `npm --version` should both respond
- **Docker Desktop** — only if you run in Docker, or use a containerized DB

> There is **no manual env-file creation** and **no manual `dotnet build`** step.
> `setup` restores dependencies **and generates every env file**
> (`frontend/.env`, `frontend/.env.docker`, and each service's
> `appsettings.Development.json`). The runner builds on launch and **hot-reloads** on edits.

---

### A. Fastest path — the `gdb` wrapper (recommended)

From this folder (`gdb-service-dotnet-main/gdb-service-dotnet-main`):

```powershell
# Windows PowerShell
.\gdb.ps1 setup            # 1. restore deps + auto-generate all env files (once per clone)
.\gdb.ps1 local inmemory   # 2. configure provider + launch everything (app + frontend)
```
```bash
# Linux / macOS / Git-Bash   (chmod +x gdb.sh first)
./gdb.sh setup
./gdb.sh local inmemory
```

That's the whole from-scratch setup. Swap `inmemory` for any provider — see **Databases** below.
Open **http://localhost:3000** and log in with **admin / Welcome@1**.

**Hot reload:** the **frontend always hot-reloads** (Vite HMR). Backend hot reload is **opt-in**,
because running all 10 services under `dotnet watch` means 10 MSBuild processes and can exhaust RAM
on small machines. Enable it with `$env:GDB_WATCH=1` (PowerShell) / `GDB_WATCH=1` (bash) before
`local`. On a tight machine, prefer running normally and watching just the service you're editing:
`cd <Service>; dotnet watch run`.

---

### B. Manual path (what the wrapper runs, step by step)

```powershell
# 1. Restore .NET + npm deps AND auto-generate all env files
dotnet run --project tools/Gdb.Setup -- all

# 2. Choose a database provider (writes appsettings.Development.json per service)
dotnet run --project tools/Gdb.Setup -- provider inmemory        # or: provider sqlite

# 3. Build once (the runner launches each service with --no-build → 10 light processes)
dotnet build

# 4. Launch all 10 services + the frontend (each in its own window)
dotnet run --project tools/Gdb.Runner
```

Expected after step 1:
```
✅ .NET Requirements installed
✅ [frontend] generated .env
✅ [frontend] generated .env.docker
✅ [frontend] npm dependencies installed
Setup Summary:
   ✓ Backend Configured: YES
   ✓ Frontend Configured: YES
```

---

## Databases

"**local**" = the app **and** the DB engine run natively on this laptop.
"**docker**" = the whole stack (app + a bundled DB container) runs in Docker.

| Provider | Local (native engine)                                    | Docker (bundled container)          |
|----------|----------------------------------------------------------|-------------------------------------|
| inmemory | `.\gdb.ps1 local inmemory`                                | `.\gdb.ps1 docker inmemory`         |
| sqlite   | `.\gdb.ps1 local sqlite`                                  | `.\gdb.ps1 docker sqlite`           |
| postgres | `.\gdb.ps1 local postgres "postgres://postgres:PWD@localhost:5432/gdb"` | `.\gdb.ps1 docker postgres` |
| mysql    | `.\gdb.ps1 local mysql "mysql://root:PWD@localhost:3306/gdb"`           | `.\gdb.ps1 docker mysql`    |
| sqlserver| `.\gdb.ps1 local sqlserver "sqlserver://sa:PWD@localhost:1433/gdb"`     | `.\gdb.ps1 docker sqlserver`|
| supabase | `.\gdb.ps1 local supabase "postgresql://postgres:PWD@db.xxxx.supabase.co:5432/postgres"` | `.\gdb.ps1 docker supabase` |

- For **local** postgres/mysql/sqlserver, install & start that engine on your laptop first, then pass **your** install's credentials in the URL. The app auto-creates the per-service DBs (`gdb_accounts_db`, …) on first boot. Passwords with special chars must be percent-encoded (`@`→`%40`).
- `supabase` is managed cloud Postgres — a connection URL is always required.
- Full cross-provider details (native installs, volume resets, smoke tests): **`TESTING_ALL_DATABASES.md`**.

### Docker — one command
```powershell
.\gdb.ps1 docker inmemory        # or postgres | mysql | sqlserver | sqlite | supabase
.\gdb.ps1 down                   # stop the stack
```
Equivalent low-level call: `dotnet run --project tools/Gdb.DockerUp -- <provider>` (`-- down` to stop).

---

## Verify
| URL | Description |
|---|---|
| http://localhost:3000 | Frontend (React / Vite) |
| http://localhost:8000 | Central Gateway |
| http://localhost:8010/health | Service Registry |
| http://localhost:8001/api/v1/docs | AccountsService Swagger |
| http://localhost:8002/api/v1/docs | TransactionsService Swagger |

Login with: **admin / Welcome@1**

### Demo data (seeded on first startup)
On the **first run** against a fresh database, AccountsService and TransactionsService
automatically seed the following (matching the Python source of truth):

| Account | Type | Holder | Privilege | Balance |
|---|---|---|---|---|
| 1000 | Savings | John Doe | GOLD | ₹50,000 |
| 1001 | Current | System Admin | PREMIUM | ₹0 |

Default PIN for both accounts: **1234**

TransactionsService seeds:
- One ₹60,000 **deposit** log for account 1001
- One ₹10,000 **transfer** (from account 1002 → 1001)

---

## Stopping
Close the service windows (or `Ctrl+C` in each), and `.\gdb.ps1 down` for the Docker stack.
Frontend changes reload live (Vite). For a **backend** change, restart that service's window
(or re-run `gdb local`) — unless you started with `GDB_WATCH=1`, in which case it reloads itself.

---

## Testing
```powershell
dotnet test -c Release
```

---

## Documentation
- **All databases (local + Docker)** → `TESTING_ALL_DATABASES.md`
- **Setup (local)** → `STUDENT_SETUP_MANUAL.html`
- **Setup (Docker)** → `DOCKER_SETUP_MANUAL.html`
- **Codebase architecture** → `CODEBASE_ARCHITECTURE_GUIDE.html`
- **Enterprise audit** → `ENTERPRISE_AUDIT_REPORT.html`
- **Remediation plan** → `ENTERPRISE_REMEDIATION_PLAN.md`

## Architecture (per stateful service)
```
<ServiceProject>/
  Controllers/    # HTTP routes
  Services/       # business logic
  Domain/         # models and interfaces
  Infrastructure/ # EF Core data access
  DTOs/           # request/response types
  Middleware/     # HTTP middleware
  Integration/    # inter-service HTTP clients
  Config/         # typed settings
```

## Configuration & env files
All env files are **auto-generated by `Gdb.Setup` — never hand-created** (they're gitignored,
so a fresh clone has none until `setup` runs):

| File | Generated for | Purpose |
|---|---|---|
| `<Service>/appsettings.Development.json` | every service | `DatabaseProvider` / `DatabaseUrl` / creds (seeded inmemory, then set per provider) |
| `frontend/.env` | frontend | `VITE_*` service URLs for local `npm run dev` |
| `frontend/.env.docker` | frontend | `VITE_*` service URLs baked into the Docker image build |

Regenerate the frontend pair anytime: `dotnet run --project tools/Gdb.Setup -- frontend-env` (add `force` to overwrite).

> **Security note:** default JWT / internal-API-key values in `appsettings.json`
> are **development placeholders**. Production configuration must be injected at
> deploy time (secrets manager / K8s Secrets) — never committed.

## License
Internal training material.

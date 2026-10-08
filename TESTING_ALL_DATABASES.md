# Testing the GDB .NET stack across all 6 databases (local + Docker)

Providers: **inmemory · sqlite · postgres · mysql · sqlserver · supabase**

Schema init per provider (after the remediation):
- **postgres / supabase** → EF Core **`Migrate()`** (versioned migrations in `<Service>/Migrations`); if that fails on a DB previously created by `EnsureCreated`, it **falls back to `EnsureCreated`** so it always boots.
- **inmemory / sqlite / mysql / sqlserver** → **`EnsureCreated()`**.

Runtime-verified so far: inmemory ✅, sqlite ✅, postgres ✅ (Migrate), mysql ✅ (`row_version`→`char(36)`). SQL Server uses the same `EnsureCreated` path; Supabase *is* Postgres.

> **Login** for every mode: `admin` / `teller` / `manager`, password **`Welcome@1`**.
> **Ports:** gateway `8000`, accounts `8001`, transactions `8002`, users `8003`, auth `8004`, aadhar `8005`, company `8006`, notification `8007`, payment `8008`, registry `8010`, frontend `3000`.

---

## A. Dockerized (recommended — one command per provider)

From `gdb-service-dotnet-main/gdb-service-dotnet-main`:

```powershell
# in-memory (no external DB; data resets on stop)
docker compose -f docker-compose.yml up --build -d

# sqlite (file DB inside each container)
docker compose -f docker-compose.yml -f docker-compose.sqlite.yml up --build -d

# postgres (bundled postgres:16 container)   ← uses Migrate()
docker compose -f docker-compose.yml -f docker-compose.postgres.yml up --build -d

# mysql (bundled mysql:8 container)
docker compose -f docker-compose.yml -f docker-compose.mysql.yml up --build -d

# sql server (bundled mssql 2022 container)
docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml up --build -d

# supabase (external managed Postgres — needs a .env with your Supabase creds)  ← uses Migrate()
docker compose -f docker-compose.yml -f docker-compose.supabase.yml up --build -d
```

Or use the helper: `dotnet run --project tools/Gdb.DockerUp -- <provider>` (e.g. `-- postgres`).

**Between provider switches, reset volumes** (avoids stale schema, esp. for the Migrate() providers):
```powershell
docker compose -f docker-compose.yml -f docker-compose.<provider>.yml down -v
```

**Open the app:** http://localhost:3000  (or the API gateway http://localhost:8000).

### ⚠ This machine is memory-tight
Running Docker **and** a .NET build together triggered an `OutOfMemoryException` earlier. If a build fails/hangs:
1. Close other apps; give Docker Desktop more RAM (Settings → Resources).
2. **Build first, then start:** `docker compose ... build`  then  `docker compose ... up -d`.
3. Reclaim space between runs: `docker image prune -f` and `docker builder prune -f`.

---

## B. Local (NO Docker at all — app **and** DB run natively on the laptop)

"Local" means the database engine is **installed as native software on your machine** (PostgreSQL / MySQL / SQL Server as real Windows services on `localhost`), not a container. Install + start the engine first, then point the app at it.

**Easiest one-liner** (uses the `gdb` wrapper):
```powershell
.\gdb.ps1 local sqlite
.\gdb.ps1 local postgres  "postgres://postgres:YOURPWD@localhost:5432/gdb"
.\gdb.ps1 local mysql     "mysql://root:YOURPWD@localhost:3306/gdb"
.\gdb.ps1 local sqlserver "sqlserver://sa:YOURPWD@localhost:1433/gdb"
.\gdb.ps1 local supabase  "postgresql://postgres:PWD@db.xxxx.supabase.co:5432/postgres"
```
Replace `YOURPWD` with the password you set when installing that DB engine. The app **auto-creates** the per-service databases (`gdb_accounts_db`, `gdb_transactions_db`, `gdb_users_db`, `gdb_auth_db`) on first boot, so the login user just needs create-database rights (the default admin user has it).

---

### The manual equivalent (what the wrapper runs for you)

**in-memory** (nothing installed):
```powershell
dotnet run --project tools/Gdb.Setup -- provider inmemory   # writes appsettings.Development.json
dotnet run --project tools/Gdb.Runner                       # launches all services + frontend
```

**sqlite** (file DB, no server to install):
```powershell
dotnet run --project tools/Gdb.Setup -- provider sqlite
dotnet run --project tools/Gdb.Runner
```

**postgres** — install PostgreSQL locally (e.g. the EDB installer / `winget install PostgreSQL.PostgreSQL`), start the `postgresql` service, then:
```powershell
dotnet run --project tools/Gdb.Setup -- postgres "postgres://postgres:YOURPWD@localhost:5432/gdb"
dotnet run --project tools/Gdb.Runner
```

**mysql** — install MySQL Server locally (`winget install Oracle.MySQL`), start the `MySQL80` service, then:
```powershell
dotnet run --project tools/Gdb.Setup -- mysql "mysql://root:YOURPWD@localhost:3306/gdb"
dotnet run --project tools/Gdb.Runner
```

**sqlserver** — install SQL Server (Express/Developer) locally, enable SQL auth + the `sa` login, start the service, then:
```powershell
dotnet run --project tools/Gdb.Setup -- sqlserver "sqlserver://sa:YOURPWD@localhost:1433/gdb"
dotnet run --project tools/Gdb.Runner
```

**supabase** — this one is *not* on your laptop; it's managed cloud Postgres. Use your project's connection URL:
```powershell
dotnet run --project tools/Gdb.Setup -- supabase "postgresql://postgres:PWD@db.xxxx.supabase.co:5432/postgres"
dotnet run --project tools/Gdb.Runner
```

`Gdb.Setup` parses the URL and writes the right host/port/db/user/password into `appsettings.Development.json` per service. The `<db>` in the URL path is ignored for the RDBMS providers — each service gets its own `gdb_<service>_db`. Standard local ports: postgres `5432`, mysql `3306`, sqlserver `1433`.

> **⚠ Passwords with special characters must be percent-encoded** in the URL (the userinfo is parsed as a URI). Encode `@`→`%40`, `:`→`%3A`, `/`→`%2F`, `#`→`%23`. E.g. a SQL Server password `P@ss:w0rd` → `sqlserver://sa:P%40ss%3Aw0rd@localhost:1433/gdb`. `Gdb.Setup` URL-decodes it back before writing the config.

> **Prefer not to install a DB engine?** Section A (Docker) bundles each database as a container so you don't have to install anything locally.

---

## C. Smoke test (any mode) — prove it works end-to-end

```powershell
# 1) login -> JWT
curl -s -X POST http://localhost:8004/api/v1/auth/login -H "Content-Type: application/json" -d '{\"login_id\":\"admin\",\"password\":\"Welcome@1\"}'

# 2) authenticated read (paste the access_token)
curl -s -H "Authorization: Bearer <TOKEN>" "http://localhost:8001/api/v1/accounts?limit=2"

# 3) gateway security (P0-2 fix): forged internal key is stripped at the edge -> 401
curl -s -o /dev/null -w "%{http_code}\n" -H "X-Internal-API-Key: dev-internal-api-key-change-in-prod" "http://localhost:8000/accounts/api/v1/internal/accounts/1000"
#   -> 401 (through gateway)   vs   200 if called directly on :8001 with the key
```

Seeded accounts: **#1000** (Savings, PIN 1234) and **#1001** (Current).

---

## C2. Hot reload & auto-generated env files

**Hot reload (local):** the **frontend always hot-reloads** (Vite dev server). **Backend hot reload is opt-in** — running all 10 services under `dotnet watch` spawns 10 MSBuild processes and **OOMs a memory-tight machine** (it also takes the frontend's esbuild down with it), so the default is a plain `dotnet run --no-build` (the `gdb local` wrapper builds once up front).
- Enable backend watch for all services (needs plenty of RAM): `GDB_WATCH=1` (PowerShell: `$env:GDB_WATCH=1`) before `gdb local`, or pass `--watch` to `Gdb.Runner`.
- **Recommended on a tight machine:** run normally, then watch only the service you're editing in its own terminal — `cd <Service> && dotnet watch run` (stop that service's plain window first to free its port).

**Env files are auto-generated — never hand-create them.** `dotnet run --project tools/Gdb.Setup -- all` (and the `gdb setup`/`local`/`docker` wrappers) generate:
- `frontend/.env` — local `npm run dev` (VITE_* service URLs → `localhost:800x`).
- `frontend/.env.docker` — Docker image build (`vite build --mode docker`); the `gdb docker` path regenerates it before building so the URLs are baked in.
- `<Service>/appsettings.Development.json` — backend config; seeded to **inmemory** if absent so the stack boots straight after `setup`, then overwritten per provider by the DB commands in §B.

All three are gitignored, which is why they're missing on a fresh clone. Regenerate the frontend pair anytime: `dotnet run --project tools/Gdb.Setup -- frontend-env` (add `force` to overwrite).

---

## D. Notes
- **Secrets:** every service now **fails closed** on empty/default secrets. The compose files and `Gdb.Setup` set `AllowInsecureDefaults=true` for the teaching stack (dev fallbacks + a `[SECURITY WARNING]` log). **Production:** set `AllowInsecureDefaults=false` and inject real `JwtSecretKey` / `InternalApiKey` / `PinEncryptionKey`, or the service won't start.
- **Non-root containers:** services run as `uid=1654(app)` with `/app` writable (SQLite file DBs work).
- **Postgres migrations:** to apply/inspect without booting: `dotnet ef database update -p AccountsService -s AccountsService --connection "Host=...;Database=...;Username=...;Password=..."` (needs `dotnet tool install --global dotnet-ef`).
- **Windows shells:** use `;` not `&&` to chain in PowerShell; backslashes in paths.

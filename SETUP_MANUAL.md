# GDB (.NET) — Setup & Run Manual

How to run the Global Digital Bank stack **locally** or with **Docker**, on **every
database provider**, in either **EF Core** or **ADO.NET** data-access mode.

> One rule to remember up front:
> **EF Core runs on every database. ADO.NET runs on SQL Server only.**

---

## 1. What you are running

| Piece | Port | Notes |
|-------|------|-------|
| Frontend (React/Vite) | 3000 | Web app — http://localhost:3000 |
| Central Gateway | 8000 | Single entry point / reverse proxy |
| **Accounts Service** | 8001 | **Owns a database** |
| **Transactions Service** | 8002 | **Owns a database** |
| **Users Service** | 8003 | **Owns a database** |
| **Auth Service** | 8004 | **Owns a database** |
| Aadhar Service | 8005 | Mock |
| Company CRV Service | 8006 | Mock |
| Notification Service | 8007 | Mock |
| Payment Gateway Service | 8008 | Mock |
| Registry Service | 8010 | Service discovery |

Only the **four bold services** touch a real database, so only those four have an
EF Core / ADO.NET switch. The mock services have no database and ignore the switch.

**Login:** `admin` / `teller` / `manager` — password `Welcome@1`.

---

## 2. Two things you choose independently

You pick these two things separately every time you run the stack:

**(a) The DATABASE PROVIDER** — where data is stored:

| Provider | Needs installing? | EF Core | ADO.NET |
|----------|-------------------|:-------:|:-------:|
| `inmemory` | No (RAM only, wiped on restart) | ✅ | ❌ |
| `sqlite`   | No (file on disk) | ✅ | ❌ |
| `postgres` | Postgres on localhost:5432 (or a container) | ✅ | ❌ |
| `mysql`    | MySQL on localhost:3306 (or a container) | ✅ | ❌ |
| `sqlserver`| SQL Server on localhost:1433 (or a container) | ✅ | ✅ |
| `supabase` | Cloud Postgres (URL required) | ✅ | ❌ |

**(b) The DATA-ACCESS MODE** — *how* the four core services read/write:

| Mode | How it's set | Works with |
|------|--------------|------------|
| **EF Core** (default) | nothing to do | every provider above |
| **ADO.NET** | `DATA_ACCESS=AdoNet` | **`sqlserver` only** |

> Choosing ADO.NET with any provider other than `sqlserver` makes the four core
> services **throw at startup on purpose** — that is expected, not a bug.

---

## 3. Prerequisites

- **.NET 10 SDK** — `dotnet --version`
- **Node.js 18+** (for the frontend) — `node --version`
- **Docker Desktop** — only if you use Docker mode
- The database engine — only if you run that provider **locally** (Docker mode
  brings its own DB container for postgres/mysql/sqlserver)

Env files (`frontend/.env`, `.env.docker`, `appsettings.Development.json`) are
**auto-generated** by setup — you never hand-write them.

---

## 4. LOCAL setup (app + DB on your laptop)

Runner: `.\gdb.ps1` (Windows PowerShell) or `./gdb.sh` (macOS/Linux/Git-Bash).
The examples use PowerShell; swap `.\gdb.ps1` → `./gdb.sh` on macOS/Linux.

### 4.1 First time only

```powershell
.\gdb.ps1 setup      # restores .NET + frontend dependencies
```

### 4.2 Run with EF Core (default) — pick a provider

```powershell
.\gdb.ps1 local inmemory
.\gdb.ps1 local sqlite
.\gdb.ps1 local postgres  "postgres://postgres:YOURPWD@localhost:5432/gdb"
.\gdb.ps1 local mysql     "mysql://root:YOURPWD@localhost:3306/gdb"
.\gdb.ps1 local sqlserver "sqlserver://sa:YOURPWD@localhost:1433/gdb"
.\gdb.ps1 local supabase  "postgresql://postgres:PWD@db.xxxx.supabase.co:5432/postgres"
```

- The 2nd argument is the provider; the 3rd (optional except supabase) is your
  DB connection URL — pass it when your local credentials differ from the default.
- The app **auto-creates** the per-service databases (`gdb_accounts_db`, …) on
  first run, so the login only needs create-database privilege (`sa` has it).
- Ctrl+C stops everything. Web app: http://localhost:3000

### 4.3 Run with ADO.NET (SQL Server only)

Set `DATA_ACCESS=AdoNet` **before** launching, then run the **sqlserver** provider:

```powershell
$env:DATA_ACCESS = "AdoNet"
.\gdb.ps1 local sqlserver "sqlserver://sa:YOURPWD@localhost:1433/gdb"
```

macOS/Linux:

```bash
DATA_ACCESS=AdoNet ./gdb.sh local sqlserver "sqlserver://sa:YOURPWD@localhost:1433/gdb"
```

To switch back to EF Core, clear the variable (or open a fresh terminal):

```powershell
Remove-Item Env:DATA_ACCESS
```

> **Tip:** run **EF Core sqlserver once first**. It creates and seeds the schema.
> ADO.NET then reads/writes that same schema (plus a few stored procedures it
> creates at startup). Running ADO.NET against an empty server still works, but
> seeding is smoothest through EF the first time.

---

## 5. DOCKER setup (whole stack in containers)

Docker mode builds every service image and, for postgres/mysql/sqlserver, starts
a **bundled DB container** too — you don't install the database yourself.

### 5.1 Run with EF Core (default) — pick a provider

Simplest, via the runner:

```powershell
.\gdb.ps1 docker inmemory
.\gdb.ps1 docker sqlite
.\gdb.ps1 docker postgres
.\gdb.ps1 docker mysql
.\gdb.ps1 docker sqlserver
.\gdb.ps1 docker supabase        # supabase = cloud DB; set its URL in .env first
```

Equivalent raw `docker compose` (what the runner does under the hood):

```powershell
docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml up --build
```

Swap `docker-compose.sqlserver.yml` for `postgres` / `mysql` / `sqlite` /
`supabase` as needed. (`inmemory` needs only the base file.)

Stop the stack:

```powershell
.\gdb.ps1 down          # or:  docker compose down
```

### 5.2 Run with ADO.NET in Docker (SQL Server only)

Stack the extra **`docker-compose.adonet.yml`** override on top of the sqlserver
provider — it sets `DATA_ACCESS=AdoNet` on the four core services:

```powershell
docker compose -f docker-compose.yml `
               -f docker-compose.sqlserver.yml `
               -f docker-compose.adonet.yml up --build
```

macOS/Linux:

```bash
docker compose -f docker-compose.yml \
               -f docker-compose.sqlserver.yml \
               -f docker-compose.adonet.yml up --build
```

Order matters: `adonet.yml` must come **last** so its `DATA_ACCESS` wins.

---

## 6. How to confirm which mode is actually running

- **Startup logs** — the four core services log their data-access mode on boot
  (`EfCore` vs `AdoNet`).
- **SQL Server (SSMS)** — in ADO.NET mode the services create stored procedures
  at startup, e.g. `usp_AccountSummary`, `usp_TransferDailyStats`, `usp_UserList`,
  `usp_GetAuthTokenByJti`. If those exist, ADO.NET ran. In EF-only mode they
  won't be there.

---

## 7. Quick reference (copy-paste)

| Goal | Command |
|------|---------|
| Local, EF, in-memory | `.\gdb.ps1 local inmemory` |
| Local, EF, SQL Server | `.\gdb.ps1 local sqlserver "sqlserver://sa:PWD@localhost:1433/gdb"` |
| Local, ADO.NET, SQL Server | `$env:DATA_ACCESS="AdoNet"; .\gdb.ps1 local sqlserver "sqlserver://sa:PWD@localhost:1433/gdb"` |
| Docker, EF, Postgres | `.\gdb.ps1 docker postgres` |
| Docker, EF, SQL Server | `.\gdb.ps1 docker sqlserver` |
| Docker, ADO.NET, SQL Server | `docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml -f docker-compose.adonet.yml up --build` |
| Stop Docker | `.\gdb.ps1 down` |

---

## 8. Troubleshooting

| Symptom | Cause / Fix |
|---------|-------------|
| Core service throws at startup with ADO.NET | You used ADO.NET with a non-sqlserver provider. ADO.NET is SQL-Server-only — switch the provider to `sqlserver` or unset `DATA_ACCESS`. |
| `DATA_ACCESS` seems ignored | It only affects the **four core services**. Mock services have no DB. Also make sure you set it in the **same** terminal before launching (local) or via the `adonet.yml` override (Docker). |
| Login fails (SQL Server) | Enable mixed-mode auth and the `sa` login; connect string uses `sa` + your password + `TrustServerCertificate=True`. |
| Can't reach DB from a container | Docker mode uses the bundled DB container automatically. If you point at a host DB, use `host.docker.internal` and the correct port. |
| Frontend build fails in Docker on a locked-down laptop | Corporate AV/proxy can break npm devDependency installs in containers — use **Local mode** for the frontend instead. |
| Stored procedures missing in SSMS | You're in EF Core mode (they're only created in ADO.NET mode), or the ADO.NET startup step hasn't run yet. |
| Service refuses to start: "insecure/empty secret(s)" | You're running outside Development without real secrets. Either run with `ASPNETCORE_ENVIRONMENT=Development` (teaching stack) or inject `JwtSecretKey` / `InternalApiKey` / `PinEncryptionKey` (see §9). |
| Service refuses to start: "AllowInsecureDefaults=true is not permitted in Production" | Exactly what it says — the dev opt-in is blocked in Production. Set it to `false` and provide real secrets. |
| `/docs` returns 404 | Swagger is served only in Development (or with `EnableSwagger=true`). In Production it is intentionally off. |
| 401 on an endpoint that used to be open | Authorization is now deny-by-default. Only health probes and key-guarded internal endpoints are anonymous; everything else needs a JWT with a staff role. |

---

## 9. Production profile (fail-closed)

The teaching stack above runs as **Development**: Swagger on, shared dev secrets allowed, demo data seeded.
Production is a separate, fail-closed profile:

```powershell
# generate real secrets once (>= 32 bytes for the JWT key)
$env:GDB_JWT_SECRET_KEY      = (openssl rand -base64 48)
$env:GDB_INTERNAL_API_KEY    = (openssl rand -base64 48)
$env:GDB_PIN_ENCRYPTION_KEY  = (openssl rand -base64 48)

docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml -f docker-compose.prod.yml up -d --build
```

What `docker-compose.prod.yml` changes:

| Setting | Teaching stack | Production profile |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Production` |
| `AllowInsecureDefaults` | `true` (dev key fallbacks) | `false` — **every service refuses to boot** without real secrets |
| Secrets | shared dev constants | from the host environment (`${VAR:?}` aborts if unset) |
| Backend ports 8001–8010 | published to the host | **not published** — the API gateway (`:8000`) is the only ingress |
| Swagger `/docs` | on | off (set `EnableSwagger=true` to re-enable deliberately) |
| Database schema | `EnsureCreated` + demo seed | versioned EF migrations applied at startup (`MigrateOnStartup`, default `true`); **no fallback, no seeding** — a migration failure stops the deploy |

> Real secrets are never committed. `appsettings.json` is fail-closed by design; the generated, gitignored
> `appsettings.Development.json` is the *only* place the dev opt-in lives.

---

## 10. Health endpoints (all four core services)

| Endpoint | Meaning | Auth |
|---|---|---|
| `GET /live` | process is up and serving HTTP (no dependency checks) | none |
| `GET /ready` | **database reachable** — every readiness check must pass; returns JSON with per-check status | none |
| `GET /api/v1/health` | legacy liveness alias | none |

Point your orchestrator's liveness probe at `/live` and its readiness probe at `/ready`. A `503` from `/ready`
means "don't route traffic here yet", not "the process is dead".

---

## 11. Operational settings added in the hardening pass

| Setting (Transactions) | Default | Purpose |
|---|---|---|
| `ReconciliationIntervalSeconds` | `60` | how often the transfer reconciler sweeps |
| `StaleTransferMinutes` | `10` | a transfer still `PENDING` this long is reported as stuck (process died mid-saga) |
| `IdempotencyReservationTtlMinutes` | `15` | orphaned `Idempotency-Key` reservations older than this are released |

| Setting (Accounts) | Default | Purpose |
|---|---|---|
| `AadhaarCryptoUpgradeOnStartup` | `true` | one-time, idempotent re-encryption of legacy Aadhaar rows to AES-GCM |

| Setting (all core services) | Default | Purpose |
|---|---|---|
| `JwtIssuer` / `JwtAudience` | `gdb-auth` / `gdb-services` | minted by AuthService, validated by every resource service — keep identical across the fleet |
| `MigrateOnStartup` | `true` | Production only: apply EF migrations at boot (set `false` when a CI/CD job owns schema changes) |
| `EnableSwagger` | `false` | serve `/docs` outside Development |

Transfers that end in `COMPENSATION_FAILED` (credit failed **and** the automatic refund failed) are logged at
**Critical** and retried by the reconciler; if they persist, they need manual reconciliation against the account
ledger — do not retry them with the same `Idempotency-Key`.

---

## 12. Scaling and observability switches (Phase 3/4 hardening)

### 12.1 Shared state store (required for more than one replica)

PIN lockouts, login throttles and transaction-history cache versions live in a **distributed cache**.
Without configuration it is an in-process store (fine for one instance and the teaching stack).
Production **refuses to start** without a shared store unless you explicitly acknowledge a single replica.

| Setting | Where | Effect |
|---|---|---|
| `Redis:ConnectionString` (env `Redis__ConnectionString`) | Accounts, Auth, Transactions | use Redis (shared by every replica) |
| `AllowSingleInstanceState=true` | same | Production may run WITHOUT Redis — only valid when exactly one replica runs |

Docker: add the Redis overlay last but one (before `docker-compose.prod.yml`):

```bash
docker compose -f docker-compose.yml -f docker-compose.postgres.yml -f docker-compose.redis.yml -f docker-compose.prod.yml up -d
```

### 12.2 Logs, metrics, traces

| Concern | How |
|---|---|
| Log format | one shared `shared/nlog.config`: human-readable in Development, **one JSON object per line** everywhere else, with `correlation_id`, `trace_id`, `user`, `method`, `path`, `exception` |
| Service name in logs | set `GDB_SERVICE_NAME` (defaults to the process name) |
| Correlation | `X-Correlation-ID` / `traceparent` are accepted, generated when missing, echoed on every response and stamped on every log line and error body |
| Prometheus | `GET /metrics` on every service (request count/duration/in-progress by route template) |
| OpenTelemetry | set `OTEL_EXPORTER_OTLP_ENDPOINT` (e.g. `http://otel-collector:4317`) — traces + metrics for ASP.NET Core and outbound HttpClient are exported over OTLP; unset = nothing registered |

### 12.3 Runtime guard rails

| Guard | Value / where |
|---|---|
| List endpoints | `skip >= 0`, `1 <= limit <= 1000` (clamped, never rejected); accounts list returns `X-Total-Count` |
| Outbound HTTP | per-try timeout 4s inside a circuit breaker (5 failures / 30s open); retries (2, jittered) **only for GET/HEAD/OPTIONS** |
| Database | pooled `DbContext`, `EnableRetryOnFailure(3)` on PostgreSQL/SQL Server, `CommandTimeout` 30s everywhere |
| Containers (`docker-compose.prod.yml`) | `restart: unless-stopped`, `mem_limit` 512m (128m frontend), 1 CPU, `stop_grace_period` 30s; the .NET GC sizes its heap from the cgroup limit |
| Docker `HEALTHCHECK` | every image probes its own `/live` (`/health` for gateway and registry) every 30s |
| Gateway behind an ingress | `TrustForwardedHeaders=true` (default in the prod override) makes rate limiting key on the real client IP via `X-Forwarded-For` |
| Request cancellation | `CancellationToken` flows from the request through services, repositories (EF + ADO.NET) and outbound HTTP; once a transfer's debit/credit has happened, the bookkeeping deliberately ignores cancellation |

### 12.4 CI/CD

Every push builds and tests the solution (warnings are errors), audits dependencies (any advisory fails),
validates all ten OpenAPI contracts, scans for secrets, builds **all ten** service images and scans each with
Trivy (fails on fixable HIGH/CRITICAL). On `main` and `v*` tags the images are published to GHCR as
`ghcr.io/<owner>/gdb/<service>:<sha>` and `:<branch-or-tag>`.

Integration tests (`Gdb.Integration.Tests`) boot the real Auth, Accounts and Transactions services in-process on the
in-memory provider and drive them over HTTP: login + throttling, deny-by-default and role checks with real tokens, the
error contract, the PIN lockout, and the transfer saga (happy path, credit failure with refund, credit + refund failure
with reconciliation flag and idempotent replay). They run with `dotnet test` like every other suite.

### 12.5 Sessions: access + refresh tokens

| Token | Lifetime | Where | Accepted at |
|---|---|---|---|
| access (`token_use=access`) | `JwtExpirationMinutes` (30) | response body, sent as `Authorization: Bearer` | every service |
| refresh (`token_use=refresh`) | `RefreshTokenDays` (7) | httpOnly, `SameSite=Strict` cookie `gdb_refresh` (or `refresh_token` in the body for non-browser clients) | **only** `POST /api/v1/auth/refresh` |

`POST /api/v1/auth/refresh` returns a new access token and **rotates** the refresh token (the one presented is
revoked; a replay is refused with 401). `POST /api/v1/auth/logout` revokes both and clears the cookie. A refresh
token presented as a Bearer token is rejected by every service. Browsers need `credentials: 'include'` on the
refresh/logout calls; the gateway already allows credentials for the configured CORS origins.

Password policy (create and change): 8-128 characters, upper + lower + digit + symbol, no spaces, must not contain
the login id.

---

## 13. Migrations: add, apply, roll back

Migrations live per service under `<Service>/Migrations` (Accounts, Transactions, Auth, Users). Design-time commands
run against PostgreSQL metadata; no database is needed to *add* a migration.

```bash
# add (after a model change) - run inside the service folder
cd TransactionsService
DatabaseProvider=postgres ASPNETCORE_ENVIRONMENT=Development AllowInsecureDefaults=true dotnet ef migrations add <Name>

# apply everything pending to a real database (or let Production do it on startup: MigrateOnStartup=true)
dotnet ef database update

# roll back: apply migrations up to (and including) an older one - every migration's Down() is executed in reverse
dotnet ef database update AddPerformanceIndexes

# remove the last migration that was never applied anywhere
dotnet ef migrations remove
```

`HasData` reference rows (transfer limits) are part of the model: changing a value produces a migration with the
diff. SQL Server databases created by `EnsureCreated` before a column existed are patched at startup by the
idempotent scripts under `Infrastructure/Data/StoredProcedures/schema_*.sql` when `DATA_ACCESS=AdoNet`.

### 13.1 Transfer rails per tier (many-to-many rule)

`transfer_limits` <-> `transfer_modes` via `transfer_limit_modes` (seeded by the model): SILVER may use NEFT/IMPS/UPI, GOLD and PREMIUM additionally RTGS. A transfer on a rail the tier may not use is refused with `400 TRANSFER_MODE_NOT_ALLOWED`; `GET /api/v1/transfer-limits/rules/all` lists `allowed_modes` per tier. An empty allow-list means no restriction.

AutoMapper (14.0.0) is used in UsersService only; its advisory is suppressed with a written justification in `Directory.Build.props`.

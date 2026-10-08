# Running Locally

How to bring up the GDB platform on your machine. The repo ships helper scripts
at the root that automate the per-service `venv` + `.env` + launch steps.

## Prerequisites

- **.NET 10.0 SDK** and **Node 18+** (for the React frontend).
- A database — or use a no-server provider (`sqlite` / `inmemory`) and skip DB setup.
- Windows PowerShell examples below; the scripts are cross-platform Python.

## One-time setup

```powershell
# 1. Choose/write .env for every service (pick ONE):
dotnet run --project tools/Gdb.Setup sqlite        # each service gets its own local .db file (no server)
dotnet run --project tools/Gdb.Setup inmemory      # pure RAM, nothing persisted (fastest; tests use this)
dotnet run --project tools/Gdb.Setup <password> <user>  # local PostgreSQL / pgAdmin
dotnet run --project tools/Gdb.Setup                  # MySQL
python setup_supabase.py               # Supabase

# 2. Create each service's virtualenv + install deps + npm install for the frontend
python Gdb.Setup
```

See [switching-databases.md](switching-databases.md) for the full provider matrix.

## Start everything

```powershell
dotnet run --project tools/Gdb.Runner            # each service + registry (:8010) + gateway (:8000) + frontend, in its own window
dotnet run --project tools/Gdb.Runner --reload   # same, with dotnet run auto-reload (handy while editing)
```

Each service launches in its **own titled console window** that stays open on
crash so you can read the error. **Fully close old windows before re-running** or
a leftover process keeps the port and the new one fails with "address already in use".

### Ports

| Service | URL |
|---|---|
| central_gateway (API gateway) | http://localhost:8000 |
| accounts_service | http://localhost:8001 |
| transactions_service | http://localhost:8002 |
| users_service | http://localhost:8003 |
| auth_service | http://localhost:8004 |
| aadhar_service | http://localhost:8005 |
| company_crv_service | http://localhost:8006 |
| notification_service | http://localhost:8007 |
| central_payment_gateway | http://localhost:8008 |
| registry_service | http://localhost:8010 |
| frontend (React) | http://localhost:3000 |

## Run a single service

Each service is a standard ASP.NET Core Web API app with its own `venv`:

```powershell
cd accounts_service
./venv/Scripts/python -m app.main            # runs dotnet run (host/port/reload from settings)
# or explicitly:
./venv/Scripts/python -m dotnet run app.main:app --port 8001 --reload
```

Interactive API docs: `http://localhost:8001/docs` (Swagger) and `/redoc`.

## Verify it's up

```powershell
curl http://localhost:8001/health     # {"status": "..."} — see operations/health-and-metrics.md
curl http://localhost:8001/live
curl http://localhost:8001/ready
curl http://localhost:8001/metrics    # Prometheus text (not in OpenAPI)
```

## Docker (optional)

Per-provider compose files exist at the root:

```powershell
dotnet run --project tools/Gdb.DockerUp                       # convenience wrapper
# or directly:
docker compose -f docker-compose.postgres.yml up
docker compose -f docker-compose.mysql.yml up
docker compose -f docker-compose.sqlite.yml up
docker compose -f docker-compose.supabase.yml up
```

## Related

- [switching-databases.md](switching-databases.md) · [testing.md](testing.md) · [adding-a-use-case.md](adding-a-use-case.md)
- Troubleshooting: [operations/troubleshooting.md](../operations/troubleshooting.md)



#!/usr/bin/env bash
# =============================================================================
# gdb.sh — one-command runner for the GDB .NET stack (Linux/macOS/Git-Bash).
# .NET equivalent of the Python setup_all.py / setup_provider.py / run_all.py /
# docker_up.py scripts. There is no venv in .NET — the equivalent of
# "create venv + pip install requirements.txt (if missing)" is "dotnet restore"
# (idempotent) + frontend "npm install", which `Gdb.Setup all` performs.
#
# LOCAL vs DOCKER:
#   local  = the app AND the database engine run natively on this laptop.
#            Postgres/MySQL/SQL Server must be INSTALLED as local services
#            (localhost:5432 / :3306 / :1433). No Docker involved.
#   docker = the whole stack (app + a bundled DB container) runs in Docker.
#
# Usage:
#   ./gdb.sh setup                     Restore .NET + frontend deps (venv + pip equivalent)
#   ./gdb.sh local  [provider] [url]   Setup + configure provider + run ALL services locally
#                                      provider: inmemory (default) | sqlite | postgres | mysql | sqlserver | supabase
#   ./gdb.sh docker <provider>         Build + start the whole stack in Docker
#   ./gdb.sh down                      Stop the Docker stack
#
# LOCAL DB credentials (override the localhost defaults with YOUR install's creds):
#   ./gdb.sh local postgres  "postgres://postgres:YOURPWD@localhost:5432/gdb"
#   ./gdb.sh local mysql     "mysql://root:YOURPWD@localhost:3306/gdb"
#   ./gdb.sh local sqlserver "sqlserver://sa:YOURPWD@localhost:1433/gdb"
#   ./gdb.sh local supabase  "postgresql://postgres:PWD@db.xxxx.supabase.co:5432/postgres"  (url REQUIRED)
# The app auto-creates the per-service databases (gdb_accounts_db, ...) on first run,
# so the login user just needs create-database privilege (the default admin user has it).
# =============================================================================
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CMD="${1:-help}"
PROVIDER="${2:-inmemory}"
URL="${3:-}"

require_dotnet() {
  command -v dotnet >/dev/null 2>&1 || { echo "[X] .NET SDK not found. Install the .NET 10 SDK."; exit 1; }
}
require_docker() {
  command -v docker >/dev/null 2>&1 || { echo "[X] Docker not found. Install Docker."; exit 1; }
  docker info >/dev/null 2>&1 || { echo "[X] Docker daemon not running. Start Docker Desktop."; exit 1; }
}
setup_deps() {
  echo "==> Restoring .NET + frontend dependencies (venv + pip-install equivalent)..."
  dotnet run --project "$ROOT/tools/Gdb.Setup" -- all
}

case "${CMD,,}" in
  setup) require_dotnet; setup_deps ;;

  local)
    require_dotnet
    echo "==> [1/4] dependencies..."; setup_deps
    echo "==> [2/4] configure provider: $PROVIDER"
    case "${PROVIDER,,}" in
      inmemory) dotnet run --project "$ROOT/tools/Gdb.Setup" -- provider inmemory ;;
      sqlite)   dotnet run --project "$ROOT/tools/Gdb.Setup" -- provider sqlite ;;
      postgres)
        [ -z "$URL" ] && { URL="postgres://postgres:postgres@localhost:5432/gdb"; echo "    (using default $URL - pass your own creds as 3rd arg if different)"; }
        dotnet run --project "$ROOT/tools/Gdb.Setup" -- postgres "$URL" ;;
      mysql)
        [ -z "$URL" ] && { URL="mysql://root:root@localhost:3306/gdb"; echo "    (using default $URL - pass your own creds as 3rd arg if different)"; }
        dotnet run --project "$ROOT/tools/Gdb.Setup" -- mysql "$URL" ;;
      sqlserver)
        [ -z "$URL" ] && { URL="sqlserver://sa:Welcome1234!@localhost:1433/gdb"; echo "    (using default $URL - pass your own creds as 3rd arg if different)"; }
        dotnet run --project "$ROOT/tools/Gdb.Setup" -- sqlserver "$URL" ;;
      supabase)
        [ -z "$URL" ] && { echo "[X] 'supabase' is managed cloud Postgres - a connection URL is REQUIRED."; echo "    ./gdb.sh local supabase \"postgresql://postgres:PWD@db.xxxx.supabase.co:5432/postgres\""; exit 1; }
        dotnet run --project "$ROOT/tools/Gdb.Setup" -- supabase "$URL" ;;
      *)
        echo "[X] Unknown provider '$PROVIDER'. Use: inmemory | sqlite | postgres | mysql | sqlserver | supabase"
        exit 1 ;;
    esac
    # Build ONCE up front so the runner launches each service with `dotnet run --no-build`
    # (10 light processes) instead of 10 concurrent MSBuilds, which OOM tight machines.
    echo "==> [3/4] building solution (once)..."
    dotnet build "$ROOT" -v q --nologo || { echo "[X] Build failed — fix errors above, then retry."; exit 1; }
    echo "==> [4/4] launching all services + frontend (Ctrl+C to stop)..."
    dotnet run --project "$ROOT/tools/Gdb.Runner" ;;

  docker)
    require_docker
    # The frontend image bakes VITE_* URLs at build time from .env.docker (COPY . .), so
    # make sure that env file exists before the image build — no manual creation.
    echo "==> Ensuring frontend env (.env.docker) exists..."
    dotnet run --project "$ROOT/tools/Gdb.Setup" -- frontend-env
    echo "==> Docker: building + starting the stack ($PROVIDER)..."
    dotnet run --project "$ROOT/tools/Gdb.DockerUp" -- "$PROVIDER" ;;

  down) require_docker; dotnet run --project "$ROOT/tools/Gdb.DockerUp" -- down ;;

  *)
    cat <<'EOF'
GDB .NET - one-command runner (local + docker)

  ./gdb.sh setup                     Restore .NET + frontend deps (venv + pip-install equivalent)
  ./gdb.sh local  [provider] [url]   App + DB both run natively on this laptop
                                       provider: inmemory (default) | sqlite | postgres | mysql | sqlserver | supabase
  ./gdb.sh docker <provider>         Whole stack (app + bundled DB container) in Docker
                                       provider: inmemory | sqlite | postgres | mysql | sqlserver | supabase
  ./gdb.sh down                      Stop the Docker stack

local (natively-installed DB) - override localhost creds with YOUR install's:
  ./gdb.sh local sqlite
  ./gdb.sh local postgres  "postgres://postgres:YOURPWD@localhost:5432/gdb"
  ./gdb.sh local mysql     "mysql://root:YOURPWD@localhost:3306/gdb"
  ./gdb.sh local sqlserver "sqlserver://sa:YOURPWD@localhost:1433/gdb"
  ./gdb.sh local supabase  "postgresql://postgres:PWD@db.xxxx.supabase.co:5432/postgres"

docker:
  ./gdb.sh docker postgres

Hot reload: the FRONTEND always hot-reloads (Vite HMR). Backend hot reload is OPT-IN
because watching all 10 services at once is RAM-heavy (can OOM small machines):
  GDB_WATCH=1 ./gdb.sh local <provider>      # watch ALL backends (needs lots of RAM)
Tip for tight RAM: run normally, then watch just the one service you're editing:
  cd <Service> && dotnet watch run
Env files (frontend/.env, .env.docker, appsettings.Development.json) are auto-generated
by setup - no manual creation. Regenerate frontend env: dotnet run --project tools/Gdb.Setup -- frontend-env

Login: admin / teller / manager  (password Welcome@1).  Web app: http://localhost:3000
EOF
    ;;
esac

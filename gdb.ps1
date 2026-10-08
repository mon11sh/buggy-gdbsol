# =============================================================================
# gdb.ps1 - one-command runner for the GDB .NET stack (Windows PowerShell).
# The .NET equivalent of the Python setup_all.py / setup_provider.py / run_all.py
# / docker_up.py scripts. ".NET has no venv" - the equivalent of
# "create venv + pip install requirements.txt (if missing)" is "dotnet restore"
# (idempotent) + "npm install" for the frontend, which `Gdb.Setup all` performs.
#
# LOCAL vs DOCKER:
#   local  = the app AND the database engine run natively on this laptop.
#            Postgres/MySQL/SQL Server must be INSTALLED as local services
#            (localhost:5432 / :3306 / :1433). No Docker involved.
#   docker = the whole stack (app + a bundled DB container) runs in Docker.
#
# Usage:
#   .\gdb.ps1 setup                       Restore .NET + frontend deps (venv + pip equivalent)
#   .\gdb.ps1 local  [provider] [url]     Setup + configure provider + run ALL services locally
#                                         provider: inmemory (default) | sqlite | postgres | mysql | sqlserver | supabase
#                                         url: connection string for a locally-installed DB (see notes below)
#   .\gdb.ps1 docker <provider>           Build + start the whole stack in Docker
#                                         provider: inmemory | sqlite | postgres | mysql | sqlserver | supabase
#   .\gdb.ps1 down                        Stop the Docker stack
#
# LOCAL DB credentials (override the localhost defaults with YOUR install's creds):
#   .\gdb.ps1 local postgres  "postgres://postgres:YOURPWD@localhost:5432/gdb"
#   .\gdb.ps1 local mysql     "mysql://root:YOURPWD@localhost:3306/gdb"
#   .\gdb.ps1 local sqlserver "sqlserver://sa:YOURPWD@localhost:1433/gdb"
#   .\gdb.ps1 local supabase  "postgresql://postgres:PWD@db.xxxx.supabase.co:5432/postgres"  (url REQUIRED)
# The app auto-creates the per-service databases (gdb_accounts_db, ...) on first run,
# so the login user just needs create-database privilege (the default admin user has it).
# =============================================================================
param(
    [Parameter(Position = 0)][string]$Command = "help",
    [Parameter(Position = 1)][string]$Provider = "inmemory",
    [Parameter(Position = 2)][string]$Url = ""
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Require-Dotnet {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Host "[X] .NET SDK not found. Install the .NET 10 SDK: https://dotnet.microsoft.com/download" -ForegroundColor Red
        exit 1
    }
}
function Require-Docker {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-Host "[X] Docker not found. Install Docker Desktop." -ForegroundColor Red; exit 1
    }
    docker info *> $null
    if ($LASTEXITCODE -ne 0) { Write-Host "[X] Docker daemon not running. Start Docker Desktop and retry." -ForegroundColor Red; exit 1 }
}
function Setup-Deps {
    Write-Host "==> Restoring .NET + frontend dependencies (venv + pip-install equivalent)..." -ForegroundColor Cyan
    dotnet run --project "$root/tools/Gdb.Setup" -- all
}

switch ($Command.ToLower()) {
    "setup" { Require-Dotnet; Setup-Deps }

    "local" {
        Require-Dotnet
        Write-Host "==> [1/4] dependencies..." -ForegroundColor Cyan
        Setup-Deps
        Write-Host "==> [2/4] configure provider: $Provider" -ForegroundColor Cyan
        switch ($Provider.ToLower()) {
            "inmemory" { dotnet run --project "$root/tools/Gdb.Setup" -- provider inmemory }
            "sqlite"   { dotnet run --project "$root/tools/Gdb.Setup" -- provider sqlite }
            "postgres" {
                if ([string]::IsNullOrWhiteSpace($Url)) { $Url = "postgres://postgres:postgres@localhost:5432/gdb"; Write-Host "    (using default $Url - pass your own creds as 3rd arg if different)" -ForegroundColor DarkGray }
                dotnet run --project "$root/tools/Gdb.Setup" -- postgres $Url
            }
            "mysql" {
                if ([string]::IsNullOrWhiteSpace($Url)) { $Url = "mysql://root:root@localhost:3306/gdb"; Write-Host "    (using default $Url - pass your own creds as 3rd arg if different)" -ForegroundColor DarkGray }
                dotnet run --project "$root/tools/Gdb.Setup" -- mysql $Url
            }
            "sqlserver" {
                if ([string]::IsNullOrWhiteSpace($Url)) { $Url = "sqlserver://sa:Welcome1234!@localhost:1433/gdb"; Write-Host "    (using default $Url - pass your own creds as 3rd arg if different)" -ForegroundColor DarkGray }
                dotnet run --project "$root/tools/Gdb.Setup" -- sqlserver $Url
            }
            "supabase" {
                if ([string]::IsNullOrWhiteSpace($Url)) {
                    Write-Host "[X] 'supabase' is a managed cloud Postgres - a connection URL is REQUIRED." -ForegroundColor Red
                    Write-Host "    .\gdb.ps1 local supabase ""postgresql://postgres:PWD@db.xxxx.supabase.co:5432/postgres""" -ForegroundColor Yellow
                    exit 1
                }
                dotnet run --project "$root/tools/Gdb.Setup" -- supabase $Url
            }
            default {
                Write-Host "[X] Unknown provider '$Provider'. Use: inmemory | sqlite | postgres | mysql | sqlserver | supabase" -ForegroundColor Red
                exit 1
            }
        }
        # Build ONCE up front so the runner can launch each service with 'dotnet run --no-build'
        # (10 lightweight processes). This avoids 10 concurrent MSBuilds, which OOM tight machines.
        Write-Host "==> [3/4] building solution (once)..." -ForegroundColor Cyan
        dotnet build "$root" -v q --nologo
        if ($LASTEXITCODE -ne 0) { Write-Host "[X] Build failed - fix errors above, then retry." -ForegroundColor Red; exit 1 }
        Write-Host "==> [4/4] launching all services + frontend (Ctrl+C to stop)..." -ForegroundColor Cyan
        dotnet run --project "$root/tools/Gdb.Runner"
    }

    "docker" {
        Require-Docker
        # The frontend image bakes VITE_* URLs at build time from .env.docker (COPY . .), so
        # make sure that env file exists before the image build - no manual creation.
        Write-Host "==> Ensuring frontend env (.env.docker) exists..." -ForegroundColor Cyan
        dotnet run --project "$root/tools/Gdb.Setup" -- frontend-env
        Write-Host "==> Docker: building + starting the stack ($Provider)..." -ForegroundColor Cyan
        dotnet run --project "$root/tools/Gdb.DockerUp" -- $Provider
    }

    "down" { Require-Docker; dotnet run --project "$root/tools/Gdb.DockerUp" -- down }

    default {
        Write-Host @'
GDB .NET - one-command runner (local + docker)

  .\gdb.ps1 setup                       Restore .NET + frontend deps (venv + pip-install equivalent)
  .\gdb.ps1 local  [provider] [url]     App + DB both run natively on this laptop
                                          provider: inmemory (default) | sqlite | postgres | mysql | sqlserver | supabase
  .\gdb.ps1 docker <provider>           Whole stack (app + bundled DB container) in Docker
                                          provider: inmemory | sqlite | postgres | mysql | sqlserver | supabase
  .\gdb.ps1 down                        Stop the Docker stack

local (natively-installed DB) - override localhost creds with YOUR install's:
  .\gdb.ps1 local sqlite
  .\gdb.ps1 local postgres  "postgres://postgres:YOURPWD@localhost:5432/gdb"
  .\gdb.ps1 local mysql     "mysql://root:YOURPWD@localhost:3306/gdb"
  .\gdb.ps1 local sqlserver "sqlserver://sa:YOURPWD@localhost:1433/gdb"
  .\gdb.ps1 local supabase  "postgresql://postgres:PWD@db.xxxx.supabase.co:5432/postgres"

docker:
  .\gdb.ps1 docker postgres

Hot reload: the FRONTEND always hot-reloads (Vite HMR). Backend hot reload is OPT-IN
because watching all 10 services at once is RAM-heavy (can OOM small machines):
  $env:GDB_WATCH=1 ; .\gdb.ps1 local <provider>      # watch ALL backends (needs lots of RAM)
Tip for tight RAM: run normally, then watch just the one service you're editing:
  cd <Service> ; dotnet watch run
Env files (frontend/.env, .env.docker, appsettings.Development.json) are auto-generated
by setup - no manual creation. Regenerate frontend env: dotnet run --project tools/Gdb.Setup -- frontend-env

Login: admin / teller / manager  (password Welcome@1).  Web app: http://localhost:3000
'@ -ForegroundColor White
    }
}

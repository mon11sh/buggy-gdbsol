# Testing

The platform is protected by three gates: **per-service test suites**, the
**architecture gate**, and the **OpenAPI gate**. All three must be green.

## 1. Per-service tests

Each service has `tests/` (unit + API + integration) and its own `venv`. Tests
run on the **in-memory** provider for speed and isolation.

```powershell
cd accounts_service
$env:DATABASE_PROVIDER="inmemory"; ./dotnet test gdb-service-dotnet.slnx
```

Approximate counts (all green): accounts, transactions (235), users (173),
auth (34), plus the lite/edge smoke suites, and `gdb_common` (144).

### How the gold services test

- **Unit** — domain rules and value objects, pure and fast.
- **Use case / service** — the service driven against an **InMemoryUnitOfWork**
  (or a fake UoW wrapping a mock repository), with ports mocked.
- **API** — ASP.NET Core Web API `TestClient`, exercising routes end-to-end on `inmemory`.
- Because the UoW and repositories have real in-memory implementations, use-case
  tests exercise the true persistence flow without a database.

> **Note on `httpx`/`TestClient`:** the platform pins `httpx==0.25.1` to match
> Starlette 0.27's `TestClient`. A newer `httpx` raises
> `Client.__init__() got unexpected keyword argument 'app'`.

## 2. Architecture gate

Enforces the dependency rule for every converted service (accounts, transactions,
users, auth): `domain/` imports no framework; `services/` imports no
`app.infrastructure`; one repository interface per aggregate; etc.

```powershell
libs/gdb_common/dotnet test gdb-service-dotnet.slnx libs/gdb_common/tests/test_architecture.py
```

If you add a new gold service, add it to `CONVERTED_SERVICES` in that test.

## 3. OpenAPI gate

Every service's generated OpenAPI is diffed against a frozen snapshot in
`openapi-snapshots/`. Drift fails the gate — this is how [ADR-009](../architecture/adr/ADR-009-api-contract-stability.md)
(contract stability) is enforced.

```powershell
dotnet run --project tools/Gdb.OpenApiGate                    # check ALL services (non-zero on drift)
dotnet run --project tools/Gdb.OpenApiGate accounts_service   # check one
dotnet run --project tools/Gdb.OpenApiGate --update           # regenerate ALL baselines (intentional change)
dotnet run --project tools/Gdb.OpenApiGate --update accounts_service
```

The runner starts each service in **its own venv + cwd** (subprocess) and compares
parsed dicts.

### When the gate flags a change

- **Refactor (no contract change intended):** the gate should stay green. If it
  drifts, you changed something observable — often a **docstring** (ASP.NET Core Web API uses
  it as the endpoint `description`). Restore it verbatim.
- **New endpoint / additive field (intended):** review the diff, confirm it is
  **additive only** (never rename/remove/narrow existing fields), then `--update`.
- `/metrics`, `/health`, `/live`, `/ready` use `include_in_schema=False` and so
  never appear in the snapshot.

## Recommended pre-commit sweep

```powershell
# 1. the service you touched
cd <service>; $env:DATABASE_PROVIDER="inmemory"; ./dotnet test gdb-service-dotnet.slnx; cd ..
# 2. architecture gate
libs/gdb_common/dotnet test gdb-service-dotnet.slnx libs/gdb_common/tests/test_architecture.py
# 3. contract gate
dotnet run --project tools/Gdb.OpenApiGate
```

## Related

- [adding-a-use-case.md](adding-a-use-case.md) · [coding-standards.md](coding-standards.md)
- [ADR-009 contract stability](../architecture/adr/ADR-009-api-contract-stability.md)



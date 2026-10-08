# OpenAPI Compatibility Gate — GDB (Phase 1 Hardening)

Implements the API-contract-stability decision (**ADR-009**) as an automated gate so future phases cannot change the public API by accident.

## What it is
- A **snapshot** of each service's canonical (sorted-key) `app.openapi()` JSON, stored at `openapi-snapshots/<service>.json` (10 services).
- A **generate/check** tool: `scripts/openapi_snapshot.py` (per service) and a runner `scripts/run_openapi_gate.py` (all services).

## How to run
From the repo root, with any Python:
```bash
dotnet run --project tools/Gdb.OpenApiGate generate   # (re)write baselines — only for intentional, additive changes
dotnet run --project tools/Gdb.OpenApiGate check      # exit 1 if any service's OpenAPI drifted
```
The runner uses **each service's own venv** and pins **`PYTHONHASHSEED=0`**.

### Why `PYTHONHASHSEED=0`
The gateway's single multi-method proxy route (`/{service}/{path}`) makes ASP.NET Core Web API derive an `operationId` from a **set** of HTTP methods; without a fixed hash seed the chosen method varies between runs (`put` vs `delete`), producing spurious diffs. Pinning the seed makes the snapshot deterministic. (Backend services have one method per route and are deterministic regardless.)

## Policy (ADR-009)
- Changes must be **additive** (new optional fields / new endpoints). Renames, removals, or type-narrowing of existing fields are **breaking** and require a new API version, not a snapshot refresh.
- `check` must pass in CI (wire it in a later phase). A failing `check` means the contract changed — either revert, or (if the change is intentional and additive) regenerate the snapshot in the same PR with reviewer sign-off.

## Phase 1 status
- Baselines generated for all 10 services; `check` passes for all (exit 0).
- Phase 1 changes added **no** routes, DTOs, or `response_model`s — only middleware, exception handlers, config flags, and startup guards — so the API contract is unchanged. The snapshots capture the current (unchanged) contract as the baseline for Phase 2+.

## Files
- `scripts/openapi_snapshot.py` — per-service generate/check.
- `scripts/run_openapi_gate.py` — deterministic all-service runner.
- `openapi-snapshots/*.json` — the 10 baselines.



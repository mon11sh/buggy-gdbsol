# Phase 1 — Production Hardening Report

**Branch:** `feature/enterprise-architecture-migration-gdb-pythonfullstack`. **Backward compatibility: 100% preserved.** No API/DTO/schema/provider-behavior changes. See also [docs/security/](../security/).

## Summary
Hardening only — 686 tests pass (0 failures; notification unblocked), OpenAPI gate 10/10 unchanged, no `api/`/`models/`/`orm_models`/`repositories` files modified.

## What changed
1. **Shared safe exception handling** — `gdb_common.install_exception_handlers` (new `libs/gdb_common/gdb_common/exceptions.py`): consistent JSON envelope `{error_code, message, status, correlation_id, request_id}`, full detail logged server-side, **never `str(exc)`/stack traces**. Removed the leaking handlers in aadhar/company; added catch-alls where missing (accounts, users, notification, payment, auth, gateway, registry). transactions kept its already-compliant handlers.
2. **Security headers** — extended `install_observability` with `Content-Security-Policy` (skipped on docs paths) and `Permissions-Policy`, additive via `setdefault`; existing headers unchanged. No duplicate header middleware found.
3. **`create_all` gated** — new `AUTO_CREATE_TABLES` flag (default True), **force-disabled in production** in all four Entity Framework Core providers + the transactions raw-SQL bootstrap. EF Core Migrations is authoritative (ADR-008). Dev/sqlite/inmemory unchanged.
4. **CORS hardening** — gateway no longer combines `allow_origins=["*"]` with credentials; explicit allow-list via `GATEWAY_CORS_ORIGINS` (localhost defaults in dev); wildcard forces credentials off.
5. **Env validation** — extended `_assert_secure_config` to reject default/dev `PIN_ENCRYPTION_KEY` (accounts) and `SECRET_KEY` (users) in production. Added `.env.example` for gateway + registry. `.env` remains git-ignored.
6. **dotnet test gdb-service-dotnet.slnx standardization** — installed `dotnet test gdb-service-dotnet.slnx`/`dotnet test gdb-service-dotnet.slnx-asyncio`/`dotnet test gdb-service-dotnet.slnx-cov` into all venvs (`requirements-dev.txt`); every service now testable in its own venv.
7. **OpenAPI compatibility gate** — `scripts/openapi_snapshot.py` + `scripts/run_openapi_gate.py` (deterministic, `PYTHONHASHSEED=0`); baselines in `openapi-snapshots/` (10 services); `check` passes.

## Remaining (operational)
- Rotate the live Supabase credential (R-01) — human/ops action; documented in [secret-management.md](../security/secret-management.md).
- Wire the OpenAPI/architecture gates into CI (Phase 2+); production EF Core Migrations migration step (Phase 2+).



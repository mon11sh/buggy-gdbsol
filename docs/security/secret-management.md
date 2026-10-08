# Secret Management — GDB (Phase 1 Hardening)

**Scope:** how GDB services load, validate, and protect secrets. Documentation of the Phase-1 controls. No secret values appear in this repo or this file.

## Principles
1. **Secrets never live in source or in git.** They come from the environment / a real `.env` (git-ignored) or, in production, a secrets manager.
2. **`.env` is ignored; `.env.example` is tracked.** `.gitignore` ignores `.env`, `*.env`, `.env.*` and un-ignores `!.env.example` (`.gitignore:11-17,531`). Every service ships a placeholder-only `.env.example`.
3. **Insecure defaults are rejected in production** via each service's `_assert_secure_config()` fail-fast guard (runs at settings import).
4. **No secret values are ever logged, printed, echoed, or committed.**

## `.env.example` coverage
All 10 services now have a placeholder-only `.env.example` (Phase 1 added the two that were missing: `central_gateway_service`, `registry_service`). These contain **only** placeholders (e.g. `YOUR-PASSWORD`, `dev-...`) — never real credentials.

## Production validation (`_assert_secure_config`)
Present and invoked at import in all 8 service Settings modules. In `ENVIRONMENT=production` it raises `RuntimeError` for unset/default secrets. Phase 1 **extended** coverage:

| Secret | Service(s) | Validated in production | Added in Phase 1 |
|---|---|---|---|
| `INTERNAL_API_KEY` | all 8 | ✅ | (existing) |
| `JWT_SECRET_KEY` | accounts, transactions, users, auth | ✅ | (existing) |
| `PIN_ENCRYPTION_KEY` | accounts | ✅ | **yes** (was unchecked — R-10) |
| `SECRET_KEY` | users | ✅ | **yes** (was unchecked — R-10) |

Gateway/registry hold no application secrets; the gateway's security-sensitive config (CORS) is validated at wire-up (see [cors-policy.md](cors-policy.md)).

## Rotated / flagged secrets (rotation log)
Phase 1 introduces no new secrets and does not print any. The following credential requires an **operational rotation** (flagged in Phase 0 as risk **R-01**):

| Variable | Location | Status | Action required |
|---|---|---|---|
| `SUPABASE_DB_PASSWORD` (and the password embedded in `SUPABASE_DATABASE_URL`) | untracked `gdb-service/.env` (git-ignored) | **live credential present in working tree** | **Rotate** the Supabase DB password out-of-band; keep `.env` git-ignored; never paste/commit/log the value. Move to a secrets manager for production. |

> This file intentionally does **not** show the value. Verify it is not committed: `git check-ignore .env` must return `.env`, and `git log -p` must contain no Supabase password.

## Operator checklist
- [ ] `.env` present locally, git-ignored, never committed.
- [ ] Real secrets injected via environment / secrets manager in production.
- [ ] Rotate `SUPABASE_DB_PASSWORD` (R-01); confirm no leak in history.
- [ ] In production set real `INTERNAL_API_KEY`, `JWT_SECRET_KEY`, `PIN_ENCRYPTION_KEY` (accounts), `SECRET_KEY` (users) — otherwise startup fails by design.
- [ ] `AUTO_CREATE_TABLES` is irrelevant to secrets but is force-disabled in production (see schema/migration policy).



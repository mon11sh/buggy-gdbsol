# CORS Policy — GDB (Phase 1 Hardening)

## Problem fixed
The API gateway previously set `allow_origins=["*"]` **with** `allow_credentials=True` (`central_gateway_service/app/main.py`). That combination is invalid per the CORS spec and unsafe (any origin could send credentialed requests). This was migration-risk **R-06**.

## Policy
1. **Never combine wildcard origins with credentials.** An explicit allow-list is used instead.
2. **Development:** explicit localhost origins by default — `http://localhost:3000` (React), `http://localhost:5173` (Vite), `http://localhost:8000` (gateway). Defined in `central_gateway_service/app/config.py::DEFAULT_CORS_ORIGINS`.
3. **Production:** the allow-list is supplied via `GATEWAY_CORS_ORIGINS` (comma-separated explicit origins). Set it to your real frontend origin(s).
4. **Defensive wire-up (startup validation):** if an operator explicitly forces `*` into the list, the gateway registers CORS with `allow_credentials=False` to stay spec-compliant (`central_gateway_service/app/main.py`). Credentials are only enabled with an explicit allow-list.

## Configuration
| Variable | Where | Default | Notes |
|---|---|---|---|
| `GATEWAY_CORS_ORIGINS` | gateway | localhost dev origins | comma-separated explicit origins; **required in production** |

`cors_origins_from_env()` (`central_gateway_service/app/config.py`) parses it; empty → localhost defaults.

## Backend services
Individual services already use explicit lists (`settings.CORS_ALLOWED_ORIGINS`), not wildcards — unchanged. The gateway is the single public entry point, so it is the primary CORS boundary.

## Verification
- Wildcard+credentials no longer possible: with default/empty `GATEWAY_CORS_ORIGINS`, the gateway uses explicit localhost origins + credentials; with `*` forced, credentials are disabled.
- No behavioral change to proxying — the 10 gateway tests remain green.



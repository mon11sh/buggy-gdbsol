# Exception Handling Policy — GDB (Phase 1 Hardening)

## Problem fixed
- `aadhar_service` and `company_crv_service` returned **`str(exc)`** from their global exception handlers — leaking internal details/stack info to clients (risk **R-07**).
- `accounts`, `users`, `notification`, `payment` had **no** global handler → unhandled errors fell to ASP.NET Core Web API's default 500.

## Shared, safe handler
A single shared installer lives in the shared library: `gdb_common.install_exception_handlers(app)` (`libs/gdb_common/gdb_common/exceptions.py`). It registers broad catch-alls only, so per-service **business/domain/auth** handlers still take precedence.

### Guarantees
- **Consistent JSON envelope**, no stack traces, never `str(exc)`:
  ```json
  {
    "error_code": "INTERNAL_ERROR",
    "message": "Internal server error",
    "status": "error",
    "correlation_id": "<X-Correlation-ID>",
    "request_id": "<X-Request-ID or correlation id>"
  }
  ```
- **Safe logging:** the full exception (type + traceback) is logged **server-side** with the correlation id; only the generic message crosses the API boundary.
- **Correlation id + request id** included for traceability (correlation id from `gdb_common` observability; request id from inbound `X-Request-ID` or the correlation id).

### Categories supported
| Category | Mechanism | Status |
|---|---|---|
| Validation | `RequestValidationError` handler — preserves ASP.NET Core Web API's native `detail` array, **adds** `error_code`/`status`/`correlation_id`/`request_id` | 422 (shape backward-compatible) |
| Authentication | per-service handlers (e.g. auth `AuthenticationException`) | 401 (unchanged) |
| Authorization | per-service role dependencies | 403 (unchanged) |
| Business / Domain | per-service exceptions (e.g. `TransactionException`, `AccountException`) | 4xx (unchanged) |
| Infrastructure | unhandled `Exception` catch-all | 500 generic |
| Unknown | unhandled `Exception` catch-all | 500 generic |

## Where it is applied
| Service | Exception handling |
|---|---|
| accounts, users, notification, payment | shared handler **added** (previously none) |
| aadhar, company | leaking `str(exc)` handler **removed**, shared handler added |
| auth | shared handler added (fills the missing generic catch-all); `AuthenticationException` handler unchanged |
| central_gateway, registry | shared handler added |
| transactions | **already compliant** — kept its existing `TransactionException` + safe generic-500 handlers (no `str(exc)` leak); left untouched to preserve its 235-test contract |

## Backward compatibility
- The generic 500 envelope is a **superset** of what services returned before (adds `correlation_id`/`request_id`; keeps `error_code`/`message`/`status`).
- The 422 handler keeps status 422 and the native `detail` array; it only **adds** fields. No test asserts the 422 body shape; tests asserting 422 **status** still pass.
- Business-exception handlers are unchanged, so their specific contracts are preserved.
- Result: **686 tests pass, 0 regressions.**



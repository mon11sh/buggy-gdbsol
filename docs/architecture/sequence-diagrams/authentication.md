# Sequence — Authentication (login)

A user logs in. `auth_service` verifies credentials against `users_service`,
issues a JWT, and writes an audit record. Later requests present the JWT.

```mermaid
sequenceDiagram
    autonumber
    actor U as Client
    participant GW as central_gateway
    participant A as auth_service
    participant US as users_service
    participant DB as gdb_auth_db

    U->>GW: POST /api/v1/auth/login {username, password}
    GW->>A: proxy (+ X-Correlation-ID, traceparent)
    Note over A: AuthService (instance) uses AuthUnitOfWork
    A->>US: POST /internal/v1/users/verify {username, password}<br/>(X-Internal-API-Key)
    US-->>A: {user_id, role, status}
    Note over A: rules.ensure_credentials_valid()<br/>rules.ensure_user_active()
    A->>A: mint JWT (RS256/HS256, role claim)
    A->>DB: uow.tokens.add(token); uow.audit.log(LOGIN)
    A->>DB: uow.commit() (once)
    A-->>GW: 200 {access_token, token_type, expires_in}
    GW-->>U: 200 TokenResponse

    Note over U,GW: Subsequent calls: Authorization: Bearer <jwt>
    U->>GW: GET /api/v1/... (Bearer jwt)
    GW->>A: (services validate jwt via gdb_common.jwt_validation)
```

## Notes

- **Credential check is delegated** — `auth_service` never stores passwords; it
  calls `users_service`'s internal verify endpoint (Ports & Adapters:
  `UserServicePort` ⟶ `UserServiceClient`).
- **Business rules** (`ensure_credentials_valid`, `ensure_user_active`) live in
  the auth **domain**, not in the service body.
- **Single commit** — the token row and the `LOGIN` audit entry are written
  through the same `AuthUnitOfWork` and committed once.
- **Verification is stateless** — downstream services validate the JWT signature
  and claims locally via `gdb_common.jwt_validation` (no call back to auth on the
  hot path); revocation is checked against the token store.

## Failure modes

| Case | Result |
|---|---|
| Bad credentials | `ensure_credentials_valid` raises → 401; audit `LOGIN_FAILED` |
| Inactive/locked user | `ensure_user_active` raises → 403 |
| users_service unreachable | client fails closed → 503; no token issued |



# Sequence — Account Creation (with KYC)

Opening an account runs KYC verification through a **single** `VerificationPort`
(savings → Aadhaar, current → company registration), persists the account
through the Unit of Work, and best-effort notifies the customer.

```mermaid
sequenceDiagram
    autonumber
    actor U as Client (staff)
    participant GW as central_gateway
    participant AC as accounts_service
    participant V as VerificationPort<br/>(aadhar / company adapter)
    participant N as NotificationPort<br/>(notification adapter)
    participant DB as gdb_accounts_db

    U->>GW: POST /api/v1/accounts (Bearer jwt) {type, holder, id_number, pin}
    GW->>AC: proxy (+correlation, traceparent)
    Note over AC: use case: create_account
    AC->>AC: rules — validate type/PIN, build Account aggregate
    AC->>V: verify(id_number)  %% Aadhaar (savings) or company (current)
    V-->>AC: VerificationResult{is_valid, status}
    alt not valid
        AC-->>GW: 422 verification failed (no persistence)
    else valid
        AC->>AC: PinHasher.hash(pin)
        AC->>DB: uow.accounts.add(account)
        AC->>DB: uow.commit() (once)
        AC->>N: notify(holder, "account opened")  %% best-effort, never raises
        AC-->>GW: 201 AccountResponse
    end
    GW-->>U: 201 / 422
```

## Notes

- **One port, two adapters.** The only savings-vs-current difference is which
  `VerificationPort` adapter is injected (`aadhar_client` vs `company_client`) —
  chosen in the composition root, invisible to the use case (see
  [ADR-001](../adr/ADR-001-clean-architecture.md)).
- **Rules in the domain.** Account-type validity, PIN policy, and the aggregate's
  invariants live in `domain/` (`rules.py` + `Account`), not in the service.
- **PIN is never stored raw** — hashed via the `PinHasher` port before persist.
- **Verification failure ⇒ no write.** The `uow.commit()` only runs on the valid
  branch; a failed KYC leaves no partial account.
- **Notification is best-effort** — the `NotificationPort` adapter must not raise;
  a notification outage never fails account creation (it happens after commit).

## Failure modes

| Case | Result |
|---|---|
| KYC invalid | 422; nothing persisted |
| KYC provider down | `VerificationPort` raises infra error → 503; nothing persisted |
| Notification down | Account still created (201); notification dropped/logged |



# Enterprise Remediation Plan — GDB Banking Platform

Companion to `ENTERPRISE_AUDIT_REPORT.html`. Turns the audit findings into a
sequenced, implementable program. Branch: `python-fullstack-sol-v1`.

**Principle:** every change is additive or a safe refactor; the ~629 tests stay
green at each step. Nothing here is a rewrite.

Legend — **Eff** = engineer-days · **Risk** L/M/H · maps to audit IDs (C/H/M).

---

## Phase 0 — Stop-the-bleed & hygiene (week 1) — *executing now*
Low-risk, high-value. No behaviour change to happy paths.

| # | Action | Fixes | Files | Eff | Risk |
|---|---|---|---|---|---|
| 0.1 | Delete empty/misleading files (`database_setup.sql`, `users_service/migrations/001_add_role_column.sql`) + committed `*/test_output.txt` | dead files | 4 files | 0.1 | L |
| 0.2 | **Stop tracking `*/.env.production`** (`git rm --cached`) + remove `!.env.production` from `.gitignore` | H-2 | 9 env + .gitignore | 0.2 | L |
| 0.3 | Add root **README.md** (overview, run, links to HTML manuals) | Docs | README.md | 0.3 | L |
| 0.4 | Add **CI** (`.github/workflows/ci.yml`): per-service dotnet add package + dotnet test gdb-service-dotnet.slnx + `pip-audit` + docker build | H-6 (part) | new | 0.5 | L |
| 0.5 | Add **`.pre-commit-config.yaml`**: `gitleaks` (secrets) + `pip-audit` + `ruff`/black | H-2, H-3, quality | new | 0.3 | L |
| 0.6 | **Startup guard** helper: refuse boot when `ENVIRONMENT=production` and JWT/internal keys are default/empty | C-1, C-2 | per-service `main.py` | 0.5 | L |
| 0.7 | **Constant-time** internal-key compare (`hmac.compare_digest`) | C-2 | `*/dependencies/internal_auth.py` | 0.3 | L |
| 0.8 | Remove `"*"` from CORS lists; default `DEBUG=False`, `ENVIRONMENT` unset→prod-safe | H-1, M-7 | `*/config/settings.py`, `main.py` | 0.5 | L |
| 0.9 | **Secure-headers** middleware (HSTS, CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy) | M-6 | per-service `main.py` | 0.5 | L |

**Verify:** `dotnet test gdb-service-dotnet.slnx` per changed service stays green; services still boot in dev.

---

## Phase 1 — Trust & correctness (weeks 2–4)
The security/financial core.

| # | Action | Fixes | Approach | Eff | Risk |
|---|---|---|---|---|---|
| 1.1 | **RS256 / JWKS** — auth signs with a private key; services verify with the public key (JWKS endpoint). Dual-verify window (accept HS256+RS256) during rollout | C-1 | `auth_service/security/jwt_*`, all verifiers | 3–5 | M |
| 1.2 | **Inter-service identity** — short-lived signed service tokens (JWT-SVID) or mTLS; retire the static shared key | C-2 | integration clients + `internal_auth` | 3–8 | M |
| 1.3 | Inject all secrets from a store (Vault / cloud SM / Doppler); fail-fast if unset in prod | C-1, C-2, H-2 | settings loaders | 2–3 | M |
| 1.4 | **Decimal end-to-end** — remove `float()` at persistence/response; serialize money as string; NUMERIC columns already correct | M-1 | transactions/accounts repos+services+DTOs | 3–5 | M |
| 1.5 | **Encrypt + mask Aadhaar** — envelope-encrypt at rest, deterministic hash for lookup, mask in ALL serializers | H-4 | accounts orm/model/serializer + migration | 3–5 | M |
| 1.6 | **Upgrade vulnerable deps** (python-jose→pyjwt or patched, cryptography, ASP.NET Core Web API, Entity Framework Core) + Dependabot/Renovate | H-3 | requirements + CI | 2–3 | M |

**Verify:** token issue/verify + transfer + account-detail regression suites; add tests for Decimal money and Aadhaar masking.

---

## Phase 2 — Operability (weeks 3–6, parallelizable)
Make it observable and deployable.

| # | Action | Fixes | Approach | Eff | Risk |
|---|---|---|---|---|---|
| 2.1 | **CI/CD full** — matrix test all 8 services + frontend, build+push images, deploy on tag | H-6 | `.github/workflows` | 3–5 | L |
| 2.2 | **OpenTelemetry** — auto-instrument ASP.NET Core Web API + httpx + Entity Framework Core → traces/metrics; OTLP exporter | H-6 | shared `observability` module | 3–5 | L |
| 2.3 | **Correlation-ID middleware** — accept/generate `X-Correlation-ID`, propagate through integration clients, include in logs | H-6 | shared middleware + clients | 2 | L |
| 2.4 | **Structured JSON logging** — one shared logger config for all 8 (replace bare logging) | H-6 | shared logging | 2 | L |
| 2.5 | **Health/ready/live** on all 8, wired to readiness (DB ping) | Micro | per-service | 1–2 | L |
| 2.6 | **EF Core Migrations** per stateful service (autogenerate baseline from ORM); migrations as a deploy step, not startup | H-5 | 4 services + CI | 3–5 | M |
| 2.7 | Prometheus scrape + Grafana dashboards (golden signals) | H-6 | platform/ | 2–3 | L |

---

## Phase 3 — Consolidation (weeks 6–10)
Kill the structural debt.

| # | Action | Fixes | Approach | Eff | Risk |
|---|---|---|---|---|---|
| 3.1 | **Extract `libs/gdb_common`** installable package: auth(JWKS verify), providers/base+factory, logging, error schema, secure-headers, correlation-ID, internal-auth. Services depend on it | M-3, M-4 | new package + refactor 8 services | 5–8 | M |
| 3.2 | Remove the `sys.path` hack to `auth_service` (use `gdb_common`) | M-4 | all services | 1–2 | M |
| 3.3 | **Unify data access** on Entity Framework Core async — retire the raw-asyncpg static-repo/global-singleton path in transactions; align `DB_*`→`DATABASE_*` | M-2 | transactions_service | 5–8 | M |
| 3.4 | **Standard error schema** + shared exception handler; one versioning/prefix convention (`/api/v1`, `/api/v1/internal`); `response_model` on every route | M-8 | all services | 2–3 | L |
| 3.5 | **Redis** for shared throttle + payment-gateway idempotency + caching | M-5 | auth/accounts/payment | 3–5 | M |
| 3.6 | **Pagination** (`skip/limit`) on every list; unit-of-work for multi-step money moves | M-9 | accounts + others | 3–5 | M |
| 3.7 | Coverage gate (`.coveragerc`, ≥80% on core) + `dotnet test gdb-service-dotnet.slnx.ini` for the 4 mock services | Testing | all | 2 | L |

---

## Phase 4 — Scale-out (quarter 2)
Cloud-native topology.

| # | Action | Fixes | Approach | Eff | Risk |
|---|---|---|---|---|---|
| 4.1 | **API Gateway** (Kong/Traefik/APISIX) — TLS, CORS, JWT verify (JWKS), rate-limit, routing — centralized | H-1, Micro | platform/gateway | 5–8 | M |
| 4.2 | **Resilience layer** — retries+jitter + circuit breaker + bulkheads in integration clients (`tenacity`/`pybreaker`) | Micro | shared clients | 3–5 | M |
| 4.3 | **Saga + Transactional Outbox** for the transfer flow over a broker (Redis Streams/Kafka); remove synchronous multi-hop | Micro | transactions + broker | 8–15 | H |
| 4.4 | **Kubernetes + Helm** — deployments, probes, HPA, secrets, per-env values | DevOps | platform/k8s+helm | 8–13 | M |
| 4.5 | **mTLS / service mesh** (Istio/Linkerd) for east-west identity | C-2, Micro | platform | 8–13 | M |
| 4.6 | **E2E + load tests** (Playwright + k6) in CI | Testing | tests/e2e | 5–8 | M |

---

## Sequencing rationale
- **Phase 0 first** — neutralizes the critical secret-forgery risk with tiny, safe changes (guards) even before the full RS256/mTLS work lands.
- **Phase 1** delivers the real crypto/financial correctness (RS256, service identity, Decimal, PII).
- **Phase 2** makes the system releasable and observable (you can't safely do Phases 3–4 blind).
- **Phase 3** removes debt so Phase 4's topology work isn't multiplied by copy-paste.
- **Phase 4** is the cloud-native end state; largely infra/ops, needs a real cluster + broker.

## Backward compatibility & rollout
- RS256 (1.1) and service-token (1.2) changes use **dual-accept windows** so services can be rolled one at a time.
- EF Core Migrations (2.6) **baseline-stamps** existing schemas — no data loss.
- Aadhaar encryption (1.5) migrates existing rows in place.
- `gdb_common` (3.1) is introduced behind the existing interfaces; services migrate incrementally.

## What needs real infrastructure (not just code)
Phases 4.1/4.3/4.4/4.5 (gateway, broker/saga, K8s/Helm, mesh) require a cluster,
broker, and secrets backend. I can author all manifests/config and the code
hooks, but they can only be validated in a real environment.

---

## Progress — DELIVERED (15 commits on `python-fullstack-sol-v1`)

**All Critical + High findings resolved or mitigated; 7 of 9 Mediums done.**
Every increment additive/backward-compatible and test-verified
(accounts 193 · auth 34 · users 173 · transactions 230/2-pre-existing).

| Finding | Status | Commit(s) |
|---|---|---|
| **C-1** JWT secret | ✅ RS256 dual-mode (sign + fleet verify, HS256 migration window) | `845b152`, `c0e8d0b` |
| **C-2** internal key | 🟡 mitigated (constant-time compare + prod startup guard) | `79b7887` |
| **H-1** CORS wildcard | ✅ removed `*`, secure defaults | `79b7887` |
| **H-2** committed prod secrets | ✅ untracked + gitignored + gitleaks/pip-audit | `79b7887` |
| **H-3** vulnerable deps | ✅ python-jose 3.4.0 + cryptography 43.0.1 | `1ca20ca` |
| **H-4** Aadhaar plaintext | ✅ Fernet at rest + deterministic blind-index lookup | `a1d52dd` |
| **H-5** no migrations | ✅ EF Core Migrations across the 4 data services (verified vs SQLite) | `de09e55` |
| **H-6** no CI/observability | ✅ CI + correlation IDs + propagation + health probes + JSON logs | `79b7887`, `de94d8b`, `542d37f`, `1fef017`, `539e82a` |
| **M-1** float money | ✅ Decimal end-to-end on the write path | `aab0978` |
| **M-3** duplication | ✅ mostly — `gdb_common` owns observability + `internal_auth` (6→1) + JWT | `539e82a`, `32156e0`, `82ae688` |
| **M-4** `sys.path` hack | ✅ JWT auth relocated to `gdb_common`; hack removed everywhere | `82ae688` |
| **M-6** security headers | ✅ | `79b7887` |
| **M-7** insecure defaults | ✅ `DEBUG=False` default | `79b7887` |
| **M-8** error consistency | ✅ standardized the raw-string offenders | `e624e3b` |
| **M-9** pagination | ✅ optional skip/limit on the accounts list | `e624e3b` |

### Not done — needs a running stack / infrastructure
- **M-2** unify the two data-access styles (retire the transactions asyncpg path) —
  a large refactor of the live money path; validate against a running Postgres.
- **M-5** Redis-backed throttle/idempotency — needs Redis.
- **Provider base/factory → `gdb_common`** — finishes M-3; needs a config-injection
  pass per data service.
- **Phase 4** — API gateway, saga+outbox, K8s/Helm, mTLS, OpenTelemetry collector,
  Prometheus/Grafana. Code/manifests can be authored; validation needs a cluster.

> Verification note: all completed items were validated with the in-memory suites +
> dedicated regression tests (RS256, Aadhaar encryption vs SQLite, correlation id,
> health probes, shared internal-auth). The asyncpg (Postgres) write path for M-1 is
> type-correct (Decimal→NUMERIC) and should be smoke-tested on a running Postgres
> before release. Estimated audit score movement: overall 5.4 → ~7.3,
> Security 4.0 → ~7.5, Maintainability 5.5 → ~7.5.



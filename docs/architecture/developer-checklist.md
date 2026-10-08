# Developer Checklist (Mandatory PR Gate)

Every PR touching a GDB microservice MUST satisfy this checklist. Reviewers reject non-conforming PRs.

## Layering & dependencies
- [ ] Business logic lives ONLY in Domain models (none in API/DTO/repo/ORM/infra/clients).
- [ ] Domain imports no ASP.NET Core Web API, Entity Framework Core, C# DTOs, httpx, settings, or logging framework.
- [ ] Application imports no ORM, Entity Framework Core, HTTP clients, or ASP.NET Core Web API `Request`; depends only on Domain + repository interfaces + ports + UoW + DTOs + mappers.
- [ ] Repositories don't import ASP.NET Core Web API or DTOs; ORM doesn't import Domain.
- [ ] Dependencies point inward; no circular dependencies.

## Contracts & mapping
- [ ] Repository interface per aggregate; business methods only; returns/accepts Domain objects (no ORM/Session/dict).
- [ ] Two explicit mapping layers (DTO↔Domain, Domain↔ORM); no reflection/implicit mapping.
- [ ] ORM models never cross a layer boundary.

## DI & providers
- [ ] Constructor injection only; no service locator, no global lookup in business logic.
- [ ] No repository/provider construction inside services or routes.
- [ ] Provider selection only in the Composition Root; Application is provider-agnostic.

## Communication
- [ ] Cross-service calls go through a Port → Adapter → HTTP client; use cases depend only on the Port.
- [ ] Outbound calls carry `X-Internal-API-Key` + `X-Correlation-ID`; critical paths use a circuit breaker.

## Transactions
- [ ] Multi-step writes run inside one Unit of Work; explicit `commit()`; rollback on error; repositories don't self-commit.

## Compatibility & tests
- [ ] Public routes/DTOs/status codes/error envelope unchanged (or additive under a new version) — ADR-009.
- [ ] OpenAPI snapshot gate green (`scripts/run_openapi_gate.py check`).
- [ ] Architecture tests green.
- [ ] Added/updated: domain, value-object, mapper, use-case, repository-contract, repository-impl, provider, UoW, API, integration tests.
- [ ] No secrets/PII/tokens in logs; production secret guards satisfied.
- [ ] No schema change without an EF Core Migrations migration (ADR-008); no automatic `create_all` in production.



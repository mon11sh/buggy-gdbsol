# GDB → .NET Web API Migration Inventory

This document contains a complete repository-wide audit of the GDB Python Fullstack project, detailing all components required to recreate the project in ASP.NET Core Web API while preserving 100% of the existing functionality, business logic, APIs, architecture, behaviors, validations, security, database structure, design patterns, and integrations.

---

## 1. Technology Stack

* **Programming Language:** Python 3.x, JavaScript / TypeScript
* **Web Framework:** ASP.NET Core Web API
* **Server:** dotnet run (ASGI)
* **ORM:** Entity Framework Core 2.0 (Async)
* **Database Drivers:** asyncpg (PostgreSQL), aiosqlite (SQLite), aiomysql (MySQL)
* **Databases:** PostgreSQL (Supabase), MySQL, SQLite, In-Memory
* **Authentication / Authorization:** JWT (RS256/HS256), Internal API Keys
* **Validation:** C# DTOs (v2)
* **Dependency Injection:** ASP.NET Core Web API `Depends`
* **HTTP Client:** HTTPX, AIOHTTP
* **Serialization:** C# DTOs JSON serialization
* **Logging:** JSON structured logging (`python-json-logger`)
* **Configuration:** C# DTOs BaseSettings (`C# DTOs-settings`), `.env` via `python-dotenv`
* **Cryptography / Security:** bcrypt, python-jose, PyJWT, cryptography, Fernet
* **Testing Framework:** dotnet test gdb-service-dotnet.slnx, dotnet test gdb-service-dotnet.slnx-asyncio
* **API Documentation:** ASP.NET Core Web API built-in Swagger UI / OpenAPI (`openapi-spec-validator`)
* **Docker:** Docker, Docker Compose
* **Database Migration Tool:** EF Core Migrations
* **Frontend Technologies:** React 18, Vite, React Router, Zustand (State Management), Tailwind CSS, React Hook Form, Zod

---

## 2. Python Packages

Extracted from all `gdb-service-dotnet.slnx / .csproj` files across services:

| Package | Version | Used By | Purpose |
|---------|---------|---------|---------|
| `ASP.NET Core Web API` | 0.104.1 | All services | Core web API framework |
| `dotnet run[standard]` | 0.24.0 | All services | ASGI web server |
| `C# DTOs` | 2.5.0 / 2.4.2 | All services | DTOs, data validation |
| `C# DTOs-settings` | 2.1.0 / 2.0.3 | All services | Environment variable configuration |
| `Entity Framework Core` | 2.0.23 | accounts, auth, transactions, users | ORM for relational DB access |
| `asyncpg` | 0.29.0 | accounts, auth, transactions, users | Async PostgreSQL / Supabase driver |
| `aiosqlite` | 0.19.0 | accounts, auth, transactions, users | Async SQLite driver |
| `aiomysql` | 0.2.0 | accounts, auth, transactions, users | Async MySQL driver |
| `greenlet` | 3.0.3 | accounts, auth, transactions, users | Required for Entity Framework Core async operations |
| `bcrypt` | 4.1.1 | accounts, auth, transactions, users | Password / PIN hashing |
| `python-jose[cryptography]` | 3.4.0 | accounts, auth, transactions, users | JWT token validation and generation |
| `PyJWT` | 2.10.1 | auth | JWT utilities |
| `cryptography` | 43.0.1 | accounts, auth, transactions, users | Secure cryptography patch |
| `httpx` | 0.25.1 | All services | Async HTTP client for cross-service calls |
| `aiohttp` | 3.9.1 | accounts, auth, transactions, users | Alternative async HTTP client |
| `python-dateutil` | 2.8.2 | accounts, transactions, users | Date parsing utilities |
| `pytz` | 2023.3 | accounts, transactions, users | Timezone handling |
| `uuid6` | 2025.0.1 | users | UUID generation |
| `python-json-logger` | 2.0.7 | accounts, transactions, users | Structured JSON logging |
| `dotnet test gdb-service-dotnet.slnx`, `dotnet test gdb-service-dotnet.slnx-asyncio`, `dotnet test gdb-service-dotnet.slnx-cov` | 7.x, 0.x, 4.x | Most services | Testing and coverage |
| `openapi-spec-validator` | 0.6.0 | accounts, transactions | Validating OpenAPI schemas |
| `python-dotenv` | 1.0.0 | All services | Loading `.env` files |
| `EF Core Migrations` | 1.18.5 | accounts, auth, transactions, users | Database migrations |
| `email-validator`, `aiofiles` | Latest | notification_service | Validating emails, async file I/O |

---

## 3. JavaScript / React Packages

Extracted from `frontend/package.json`:

| Package | Version | Purpose |
|---------|---------|---------|
| `react`, `react-dom` | 18.2.0 | Core UI library |
| `vite` | 5.0.8 | Build tool and dev server |
| `react-router-dom` | 6.21.0 | Client-side routing |
| `zustand` | 4.4.7 | State management |
| `react-hook-form` | 7.71.1 | Form state management |
| `zod`, `@hookform/resolvers` | 4.3.6, 5.2.2 | Form validation |
| `axios` | 1.13.4 | HTTP client |
| `lucide-react` | 0.303.0 | Iconography |
| `recharts` | 2.10.3 | Charting and data visualization |
| `tailwindcss`, `autoprefixer`, `postcss` | 3.4.0 | Utility-first CSS styling |
| `clsx` | 2.0.0 | Conditional class joining |
| `date-fns` | 3.0.6 | Date manipulation |
| `react-hot-toast` | 2.4.1 | Toast notifications |

---

## 4. Shared Libraries

All shared components are located in `libs/gdb_common`.

* **`auth_dependencies.py`**: Provides ASP.NET Core Web API dependencies (`Depends`) for RBAC and JWT validation. Examples: `require_role`, `require_admin_or_teller`.
* **`jwt_validation.py`**: Validates incoming JWTs (supports RS256 and HS256 fallbacks).
* **`internal_auth.py`**: Middleware/Dependency for service-to-service authentication using `X-Internal-API-Key` (via constant-time HMAC comparison).
* **`cqrs.py`**: Base implementation of a Command Query Responsibility Segregation (CQRS) mediator/bus. Used heavily by the `transactions_service`.
* **`resilience.py`**: Implements a Circuit Breaker pattern to prevent cascading failures across inter-service HTTP calls.
* **`ratelimit.py`**: In-memory token bucket rate limiter (used primarily in the API Gateway).
* **`discovery.py`**: Service registry client for dynamic service discovery (fallback to static config if registry fails).
* **`observability.py`**: Implements ContextVar-based Correlation IDs (`X-Correlation-ID`) and structured JSON logging.
* **`exceptions.py`**: Base HTTP exceptions providing standard `error_code` and `http_code`.

---

## 5. External Services

* **Databases**: Supabase (PostgreSQL), MySQL, SQLite.
* **Mocked Third-Party Services**: 
  * `aadhar_service` (Govt ID verification)
  * `company_crv_service` (Company registration validation)
  * `notification_service` (Email/SMS simulation)
  * `payment_gateway_service` (External payment processing simulation)

---

## 6. Design Patterns

* **Repository Pattern**: Extensively used across services (e.g., `AccountRepository`, `TransactionRepositoryInterface`) to abstract database operations.
* **Strategy Pattern**: Used for database providers (`Entity Framework CoreProvider`, `InMemoryProvider`, `AsyncpgProvider`) and account types (`SavingsImpl`, `CurrentImpl`).
* **Factory Pattern**: `create_provider()` to instantiate the correct DB provider; `AccountFactory` for account implementations.
* **CQRS / Mediator Pattern**: Implemented in `transactions_service` via `gdb_common/cqrs.py` to separate command and query flows.
* **Circuit Breaker Pattern**: Used in inter-service clients (e.g., `account_service_client.py`) to wrap HTTP calls.
* **API Gateway Pattern**: `central_gateway_service` routes external traffic to internal microservices.
* **Singleton Pattern**: Database providers and inter-service HTTP clients are memoized as singletons.

---

## 7. Architecture

* **Architecture Style**: Microservices (8 business services, 1 gateway, 1 registry) + React Frontend.
* **Request Flow**: Client -> `central_gateway_service` -> Specific Backend Service -> Database.
* **Layering**: Layered within each service (`api` -> `services` -> `repositories` -> `database`).
* **Inter-service Communication**: Synchronous HTTP via `HTTPX` using Internal API Keys and Correlation IDs.
* **Dependency Injection**: Achieved purely via ASP.NET Core Web API's `Depends` and manual factories (no DI container library like Punq or Dependency Injector).
* **DTOs**: Handled via C# DTOs (`@dataclass` and `BaseModel`). No rich domain models exist (Anemic Domain Model).
* **Exception Handling**: Mix of per-route `try/except` and global exception handlers (e.g., `transactions_service`).

---

## 8. Database

* **Providers**: SQLite, MySQL, PostgreSQL (Supabase), In-Memory.
* **ORM**: Entity Framework Core 2.0 (using `DeclarativeBase`, `Mapped`, `mapped_column`) and raw `asyncpg`.
* **Schema Generation**: Relies on `Base.metadata.create_all()` during startup scripts and pure SQL files (`*_schema.sql`), not EF Core Migrations migrations directly at runtime.
* **Migrations**: Scaffolded with EF Core Migrations (`EF Core Migrations` folder present), but manual SQL is heavily utilized.
* **Database Isolation**: Database-per-service (e.g., `gdb_accounts_db`, `gdb_users_db`, `gdb_auth_db`, `gdb_transactions_db`).

---

## 9. Security

* **JWT**: Access tokens issued by `auth_service` (RSA if keys provided, otherwise HMAC). Claims include `sub`, `login_id`, `role`, `jti`.
* **RBAC**: Handled via custom ASP.NET Core Web API dependencies injecting the current user and checking `role`.
* **Internal Authentication**: `X-Internal-API-Key` header verified using `hmac.compare_digest`.
* **Encryption / Hashing**: `bcrypt` (12 rounds) for PINs/Passwords. Fernet symmetric encryption for sensitive PII (Aadhaar).
* **Blind Indexing**: A cryptographic hash of the Aadhaar number is stored for searchability without exposing the raw value.
* **Security Headers**: Injected via middleware (`X-Content-Type-Options`, `X-Frame-Options`, `HSTS`, `Referrer-Policy`).

---

## 10. Middleware

* **Observability Middleware**: Extracts or generates `X-Correlation-ID`, sets it in a ContextVar, and injects security headers.
* **CORS Middleware**: Standard ASP.NET Core Web API CORS setup; wildcard `*` used in the gateway (needs tightening).
* **Rate Limiting Middleware**: Custom token-bucket rate limiter located in `central_gateway_service`.
* **TrustedHostMiddleware**: Used selectively (e.g., in `accounts_service`).

---

## 11. Configuration

* **Environment Variables**: Managed via `.env` files.
* **Settings Classes**: C# DTOs `BaseSettings` (`config/settings.py`) duplicated across every service.
* **Startup Logic**: Python scripts (`setup_mysql.py`, `setup_supabase.py`, `run_all.py`, `docker_up.py`) handle orchestrating environments, applying SQL schemas, and launching dotnet run servers.

---

## 12. Testing

* **Frameworks**: `dotnet test gdb-service-dotnet.slnx`, `dotnet test gdb-service-dotnet.slnx-asyncio`.
* **Mocking**: `unittest.mock.AsyncMock`, `MagicMock`, HTTPX `MockTransport`.
* **Fixtures**: Uses `conftest.py` extensively. Overrides ASP.NET Core Web API dependencies using `app.dependency_overrides`.
* **Coverage**: `dotnet test gdb-service-dotnet.slnx-cov` installed but no strict coverage enforcement.

---

## 13. DevOps

* **Docker**: Standalone `Dockerfile` for each microservice and the frontend.
* **Docker Compose**: Base `docker-compose.yml` (in-memory) with overrides (`docker-compose.mysql.yml`, `docker-compose.postgres.yml`, `docker-compose.supabase.yml`).
* **CI/CD**: `.github` workflows likely exist for standard checks.
* **Startup Scripts**: Extensive custom Python setup scripts in `scripts/` and the root folder for local environment bootstrapping.

---

## 14. Exact .NET Migration Mapping

| Existing Technology (Python/React) | Purpose | ASP.NET Core Equivalent | Notes |
|------------------------------------|---------|-------------------------|-------|
| ASP.NET Core Web API | Web Framework | **ASP.NET Core Web API** | Use Controllers or Minimal APIs. Minimal APIs align closely with ASP.NET Core Web API routing. |
| dotnet run | Web Server | **Kestrel** | Built-in to ASP.NET Core. |
| C# DTOs | DTOs & Validation | **Records / Classes + FluentValidation or DataAnnotations** | C# Records are ideal for immutable DTOs. |
| Entity Framework Core / asyncpg | ORM & DB Access | **Entity Framework Core (EF Core)** | Supports SQL Server, Postgres (Npgsql), MySQL (Pomelo), SQLite, and In-Memory. |
| EF Core Migrations | Database Migrations | **EF Core Migrations** | Native, code-first migration approach. |
| ASP.NET Core Web API `Depends` | Dependency Injection | **Microsoft.Extensions.DependencyInjection** | Built-in IoC container (`AddScoped`, `AddSingleton`, `AddTransient`). |
| httpx / aiohttp | HTTP Client | **IHttpClientFactory / HttpClient** | Built-in to .NET. |
| bcrypt | Hashing | **BCrypt.Net-Next** or **ASP.NET Core Identity PasswordHasher** | `BCrypt.Net-Next` is a direct port. |
| PyJWT / python-jose | JWT Generation & Validation | **System.IdentityModel.Tokens.Jwt** + **Microsoft.AspNetCore.Authentication.JwtBearer** | Standard .NET auth middleware. |
| Cryptography (Fernet) | Symmetric Encryption | **System.Security.Cryptography.Aes** | Built-in AES encryption. |
| asyncio | Asynchronous I/O | **Task / async/await** | Native C# async support. |
| python-json-logger | Structured Logging | **Serilog** | Industry standard for structured JSON logging in .NET. |
| gdb_common CQRS | Command/Query Bus | **MediatR** | The standard .NET library for CQRS and Mediator patterns. |
| gdb_common Resilience | Circuit Breaker & Retries | **Polly** | Comprehensive .NET resilience and fault-handling library. |
| central_gateway_service | API Gateway | **YARP (Yet Another Reverse Proxy) or Ocelot** | YARP is the modern, highly performant standard for .NET gateways. |
| gdb_common RateLimit | Rate Limiting | **Microsoft.AspNetCore.RateLimiting** | Built-in to .NET 7+. |
| Service Discovery | Registry | **Steeltoe / Consul / Kubernetes DNS** | Alternatively, rely on YARP + static config. |
| dotnet test gdb-service-dotnet.slnx | Testing | **xUnit + Moq / NSubstitute + FluentAssertions** | Standard .NET testing stack. |
| ASP.NET Core Web API TestClient | API Testing | **WebApplicationFactory** | Standard integration testing in .NET. |

---

## 15. Potential Migration Challenges

1. **Dependency Injection Differences**:
   * ASP.NET Core Web API's `Depends` allows resolution at the route level based on request context natively and effortlessly.
   * **Challenge**: .NET relies heavily on constructor injection. Scoped services must be carefully managed. The `AccountService` anti-pattern (calling `get_provider()` directly inside the class) must be refactored to use standard constructor injection in .NET.

2. **Entity Framework Core vs Entity Framework Core**:
   * Entity Framework Core acts dynamically. EF Core is strongly typed and requires rigid DB contexts.
   * **Challenge**: Translating the dynamic "Provider Strategy" (where a service swaps between Postgres, MySQL, and SQLite at runtime) requires configuring EF Core `DbContextOptionsBuilder` dynamically at startup based on configuration.

3. **CQRS & MediatR**:
   * The Python codebase uses a custom, lightweight CQRS bus.
   * **Challenge**: Moving to `MediatR` is standard, but the behavior pipelines must be mapped correctly. Ensure commands and queries do not cross boundaries.

4. **Exception Handling Middleware**:
   * Python uses ASP.NET Core Web API's `@app.exception_handler`.
   * **Challenge**: Implement an `IExceptionHandler` (in .NET 8) or custom Exception Handling Middleware to map exceptions (like `TransactionException`) to standardized `ProblemDetails` responses.

5. **Async Cryptography**:
   * The Python app runs `bcrypt` on the event loop (which blocks).
   * **Challenge**: .NET `Task.Run` can be used to offload CPU-bound bcrypt hashing to the thread pool to avoid thread starvation, improving on the original implementation's performance.

6. **Inter-Service Communication**:
   * The Python app uses `HTTPX` with injected Correlation IDs.
   * **Challenge**: Configure `IHttpClientFactory` with `DelegatingHandler`s to automatically propagate `X-Correlation-ID` and `X-Internal-API-Key` on outgoing requests across all services.

7. **API Gateway (YARP)**:
   * The `central_gateway_service` has custom rate limiting and routing.
   * **Challenge**: Configuring YARP routes and clusters via `appsettings.json`, and attaching ASP.NET Core RateLimiting middleware to YARP endpoints.

8. **CORS and Security Headers**:
   * Standardizing security headers across 8 .NET applications requires creating a shared NuGet package or class library (equivalent to `gdb_common`) containing custom ASP.NET Core middleware.

---

## 16. Final Deliverable Assessment

This inventory confirms the presence of an 8-backend microservice architecture using synchronous HTTP, custom CQRS, circuit breakers, and multiple database providers. 

To recreate this identically in **ASP.NET Core Web API**:
1. Create a `.sln` with **8 Web API projects**, **1 YARP Gateway project**, and **1 Shared Class Library** (`Gdb.Common`).
2. Implement **MediatR** for the Transactions service CQRS.
3. Use **EF Core** with dynamic provider selection for the repositories.
4. Implement **Polly** for the `account_service_client` Circuit Breaker.
5. Use **WebApplicationFactory** for porting the integration tests.
6. Centralize JWT Bearer Authentication and RBAC Policies in the Shared Class Library.

No architectural shifts are necessary for this port; it is a 1:1 migration utilizing standard Microsoft / .NET OSS equivalents.



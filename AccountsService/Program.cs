using AccountsService.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using NLog.Web;
using System.Text;
using AccountsService.Integration;
using AccountsService.Infrastructure.Data;
using AccountsService.Infrastructure.Repositories;
using AccountsService.Services;
using AccountsService.Utils;
using Microsoft.EntityFrameworkCore;

using Gdb.Common.Security;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Host.UseNLog();

// Bind settings
var settings = new Settings();
builder.Configuration.Bind(settings);
// Honour the DATA_ACCESS environment variable as an override for the data-access strategy
// ("EfCore" default | "AdoNet"). Config binding maps top-level keys, so read the underscored
// env var name explicitly here.
var dataAccessEnv = Environment.GetEnvironmentVariable("DATA_ACCESS");
if (!string.IsNullOrWhiteSpace(dataAccessEnv))
    settings.DataAccess = dataAccessEnv;
// Honour ASPNETCORE_URLS when the host/orchestrator sets it (containers, CI, the OpenAPI gate);
// fall back to the configured Host/Port only for plain local runs.
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    builder.WebHost.UseUrls($"http://{settings.Host}:{settings.Port}");
builder.Services.AddSingleton(settings);
// /live (process up) and /ready (database reachable) via the framework health-check subsystem.
Gdb.Common.Health.GdbHealthExtensions.AddDatabase<AppDbContext>(Gdb.Common.Health.GdbHealthExtensions.AddGdbHealthChecks(builder.Services));

// Fail-closed secret validation — runs in EVERY environment (see Gdb.Common.Security.SecureConfigGuard).
// With AllowInsecureDefaults=false (the default), empty/default secrets refuse to start.
// The dev opt-in is never valid in Production, regardless of what config says.
SecureConfigGuard.AssertProductionSafe(builder.Environment.IsProduction(), settings.AllowInsecureDefaults);
SecureConfigGuard.Assert(settings.AllowInsecureDefaults,
    ("JwtSecretKey", settings.JwtSecretKey),
    ("InternalApiKey", settings.InternalApiKey),
    ("PinEncryptionKey", settings.PinEncryptionKey));
if (settings.AllowInsecureDefaults)
{
    // Dev/teaching opt-in: fill any empty/default secret with the shared dev fallback so
    // cross-service JWT and internal-key checks stay consistent.
    settings.JwtSecretKey = SecureConfigGuard.OrDevDefault(settings.JwtSecretKey, SecureConfigGuard.DevJwtSecretKey);
    settings.InternalApiKey = SecureConfigGuard.OrDevDefault(settings.InternalApiKey, SecureConfigGuard.DevInternalApiKey);
    settings.PinEncryptionKey = SecureConfigGuard.OrDevDefault(settings.PinEncryptionKey, SecureConfigGuard.DevPinEncryptionKey);
}
// Auth kill-switch is a local-dev convenience only; never allow it without the insecure opt-in.
if (settings.DisableAuth && !settings.AllowInsecureDefaults)
    throw new InvalidOperationException("DisableAuth=true requires AllowInsecureDefaults=true (local development only).");

// Add Database Context
builder.Services.AddDbContextPool<AppDbContext>(options => {
    var dbProvider = settings.DatabaseProvider.ToLower();
    
    string connectionString = settings.DatabaseUrl;
    if (string.IsNullOrEmpty(connectionString))
    {
        if (dbProvider == "postgres" || dbProvider == "supabase")
            connectionString = $"Host={settings.DatabaseHost};Port={settings.DatabasePort};Database={settings.DatabaseName};Username={settings.DatabaseUser};Password={settings.DatabasePassword}";
        else if (dbProvider == "mysql")
            connectionString = $"Server={settings.DatabaseHost};Port={settings.DatabasePort};Database={settings.DatabaseName};User={settings.DatabaseUser};Password={settings.DatabasePassword}";
        else if (dbProvider == "sqlserver" || dbProvider == "mssql")
            connectionString = $"Server={settings.DatabaseHost},{settings.DatabasePort};Database={settings.DatabaseName};User Id={settings.DatabaseUser};Password={settings.DatabasePassword};TrustServerCertificate=True;{Gdb.Common.Data.ConnectionPooling.SqlServer}";
    }

    switch (dbProvider)
    {
        case "postgres":
        case "supabase":
            options.UseNpgsql(connectionString, o => o.EnableRetryOnFailure(3).CommandTimeout(30)).UseSnakeCaseNamingConvention();
            break;
        case "mysql":
            options.UseMySQL(connectionString, o => o.CommandTimeout(30)).UseSnakeCaseNamingConvention();
            break;
        case "sqlserver":
        case "mssql":
            options.UseSqlServer(connectionString, o => o.EnableRetryOnFailure(3).CommandTimeout(30)).UseSnakeCaseNamingConvention();
            break;
        case "sqlite":
            options.UseSqlite(string.IsNullOrEmpty(connectionString) ? "Data Source=accounts.db" : connectionString, o => o.CommandTimeout(30)).UseSnakeCaseNamingConvention();
            break;
        case "inmemory":
            options.UseInMemoryDatabase("InMemoryDb");
            break;
        default:
            options.UseInMemoryDatabase("InMemoryDb");
            break;
    }
});

// Add CORS
var allowedOrigins = settings.CorsAllowedOrigins.Split(',').Select(o => o.Trim()).ToArray();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

// Add Authentication
if (!settings.DisableAuth)
{
    if (!string.Equals(settings.JwtAlgorithm, "RS256", StringComparison.OrdinalIgnoreCase))
        SecureConfigGuard.AssertMinimumKeyLength("JwtSecretKey", settings.JwtSecretKey);
    builder.Services.AddGdbJwtAuthentication(settings.JwtSecretKey, settings.JwtAlgorithm, settings.JwtPublicKey,
        settings.JwtIssuer, settings.JwtAudience);

    
}


builder.Services.AddControllers(options => options.Filters.Add<Gdb.Common.Filters.FastApiValidationFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    });
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options => 
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower);
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
builder.Services.AddEndpointsApiExplorer();
Gdb.Common.Swagger.GdbSwaggerExtensions.AddGdbSwagger(builder.Services, "GDB Accounts Service API");

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<AccountsService.Mapping.AccountResponseMapper>();

// Register Dependencies
// Data-access toggle: AdoNet (SQL Server only) or EF Core (default). AddDbContext above stays
// registered regardless; when AdoNet is selected the EF context is simply unused for repository ops.
if (string.Equals(settings.DataAccess, "AdoNet", StringComparison.OrdinalIgnoreCase))
{
    var _adoProvider = settings.DatabaseProvider.ToLowerInvariant();
    if (_adoProvider != "sqlserver" && _adoProvider != "mssql")
        throw new InvalidOperationException(
            $"DataAccess=AdoNet is supported only with the 'sqlserver' provider; current provider is '{settings.DatabaseProvider}'.");
    builder.Services.AddScoped<IAccountRepository, AdoNetAccountRepository>();
}
else
{
    builder.Services.AddScoped<IAccountRepository, AccountRepository>();
}
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IAccountInternalService, AccountInternalService>();
builder.Services.AddSingleton<IPinLockoutService, PinLockoutService>();
builder.Services.AddSingleton<EncryptionManager>();
// Process-wide account-list cache. MUST be a singleton: it owns a Timer and implements the full
// IDisposable + finalizer pattern; registering it scoped/transient would let the DI container
// dispose it per request, and later reads would throw ObjectDisposedException.
builder.Services.AddSingleton<AccountsService.Utils.AccountListCache>();

// Register HttpClients and Circuit Breakers
builder.Services.AddHttpClient<IAadharClient, AadharClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
    // BaseAddress is set per request now
})
.AddPolicyHandler(Gdb.Common.Integration.PollyPolicies.CreateSelector());

builder.Services.AddHttpClient<ICompanyClient, CompanyClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
    // BaseAddress is set per request now
})
.AddPolicyHandler(Gdb.Common.Integration.PollyPolicies.CreateSelector());

builder.Services.AddHttpClient<INotificationClient, NotificationClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
    // BaseAddress is set per request now
})
.AddPolicyHandler(Gdb.Common.Integration.PollyPolicies.CreateSelector());

if (settings.ServiceDiscoveryEnabled)
{
    builder.Services.AddSingleton<Gdb.Common.Discovery.RegistryResolver>(sp => 
        new Gdb.Common.Discovery.RegistryResolver(
            sp.GetRequiredService<IHttpClientFactory>(),
            settings.RegistryUrl,
            null,
            10.0,
            2.0
        )
    );
    Gdb.Common.Discovery.ServiceDiscoveryExtensions.AddGdbServiceDiscovery(builder.Services, settings.DiscoveryName, settings.InstanceUrl, settings.RegistryUrl);
}

builder.Services.AddHttpContextAccessor();

// Use the guard-RESOLVED key (dev default in dev, injected real key in prod), NOT the raw config
// value — appsettings has InternalApiKey="" under the fail-closed model, so raw config left the
// [InternalApi] filter with an empty expected-key and rejected every internal account lookup.
builder.Services.AddSingleton<Func<string>>(() => settings.InternalApiKey);
Gdb.Common.Middleware.GdbMiddlewareExtensions.AddGdbCrossCuttingMiddleware(builder.Services);
Gdb.Common.Observability.GdbOpenTelemetryExtensions.AddGdbOpenTelemetry(builder.Services, "accounts-service");
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30)); // drain in-flight requests before the container is killed
Gdb.Common.Caching.GdbDistributedCacheExtensions.AddGdbDistributedCache(builder.Services, builder.Configuration, builder.Environment, "accounts-service");

var app = builder.Build();

// Cross-cutting: correlation/trace id + security response headers (outermost).
Gdb.Common.Middleware.GdbMiddlewareExtensions.UseGdbCrossCuttingMiddleware(app);


app.UsePathBase(settings.ApiPrefix);

// Served only in Development (or EnableSwagger=true) — never disclosed by default in Production.
Gdb.Common.Swagger.GdbSwaggerExtensions.UseGdbSwagger(app, "GDB Accounts Service API", settings.ApiPrefix);


app.UseCors("AllowSpecificOrigins");

if (!settings.DisableAuth)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapControllers();
Gdb.Common.Middleware.MetricsEndpointExtensions.MapGdbMetrics(app);
// /live and /ready (database-probing) — this service previously exposed neither.
Gdb.Common.Health.GdbHealthExtensions.MapGdbHealthEndpoints(app);

// Initialize DB if requested
// Production: apply versioned migrations on startup — no EnsureCreated fallback and no seeding, so a
// migration failure stops the deploy instead of silently producing a drifted schema. Opt out with
// MigrateOnStartup=false when a CI/CD job owns schema changes.
if (app.Environment.IsProduction() && settings.MigrateOnStartup)
{
    var _prodProvider = settings.DatabaseProvider.ToLowerInvariant();
    if (_prodProvider == "postgres" || _prodProvider == "supabase")
    {
        using var _migrateScope = app.Services.CreateScope();
        _migrateScope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    }
}

// Development/teaching stack only (both the custom Environment setting AND the host environment must
// agree it is not Production): create the schema and seed demo data.
if (settings.AutoCreateTables && !settings.Environment.Equals("production", StringComparison.OrdinalIgnoreCase) && !app.Environment.IsProduction())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // postgres/supabase -> versioned EF Core migrations (AccountsService/Migrations);
    // dev providers (sqlite/inmemory/mysql/sqlserver) -> EnsureCreated
    // (TODO(P3-1): per-provider migration sets for mysql/sqlserver).
    var _dbp = settings.DatabaseProvider.ToLower();
    if (_dbp == "postgres" || _dbp == "supabase")
    {
        try
        {
            dbContext.Database.Migrate();
        }
        catch (Exception _mex)
        {
            // Fallback so the demo/tests stay bootable across all providers even if this DB was
            // previously created by EnsureCreated (no migration history). Prod starts from a clean DB.
            NLog.LogManager.GetCurrentClassLogger().Warn(_mex, "Migrate() failed; falling back to EnsureCreated");
            dbContext.Database.EnsureCreated();
        }
    }
    else
        dbContext.Database.EnsureCreated();

    if (!dbContext.Accounts.Any())
    {
        NLog.LogManager.GetCurrentClassLogger().Info("Seeding default accounts...");
        var encryptionManager = scope.ServiceProvider.GetRequiredService<AccountsService.Utils.EncryptionManager>();

        // Python seed_default_accounts: DEFAULT_PIN = "1234", ACCOUNT_NUMBER_START = 1000.
        // Python hashes the PIN directly via bcrypt and bypasses validate_pin by calling
        // uow.accounts.add_savings() directly (not the service use case layer).
        // Mirror that here: pre-hash the PIN and write directly to the DbContext.
        const string defaultPin = "1234";
        string pinHash = BCrypt.Net.BCrypt.HashPassword(defaultPin, workFactor: 12);

        // Account 1000 — Savings, John Doe, GOLD, ₹50,000 (Python: ACCOUNT_NUMBER_START = 1000)
        const string aadharNumber = "123456789012";
        string encryptedAadhar = encryptionManager.EncryptData(aadharNumber);
        string aadharHash = encryptionManager.GenerateBlindIndex(aadharNumber);

        var savingsAccount = new AccountsService.Infrastructure.Data.Entities.AccountEntity
        {
            AccountNumber = 1000,
            AccountType = "SAVINGS",
            Name = "John Doe",
            Privilege = "GOLD",
            PinHash = pinHash,
            Balance = 50000.0m,
            BankName = "Global Digital Bank",
            BankBranch = "Main Branch",
            IfscCode = "GDB0000001",
            IsActive = true,
            ActivatedDate = DateTime.UtcNow
        };
        dbContext.Accounts.Add(savingsAccount);
        dbContext.SaveChanges();

        var savingsDetails = new AccountsService.Infrastructure.Data.Entities.SavingsAccountDetailsEntity
        {
            AccountNumber = 1000,
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = "Male",
            PhoneNo = "9876543210",
            AadharNumber = encryptedAadhar,
            AadharHash = aadharHash
        };
        dbContext.SavingsAccountDetails.Add(savingsDetails);
        dbContext.SaveChanges();

        // Account 1001 — Current, System Admin, PREMIUM, ₹0 (second account)
        const string regNo = "U12345MH2020PTC123456";
        var currentAccount = new AccountsService.Infrastructure.Data.Entities.AccountEntity
        {
            AccountNumber = 1001,
            AccountType = "CURRENT",
            Name = "System Admin",
            Privilege = "PREMIUM",
            PinHash = pinHash,
            Balance = 0.0m,
            BankName = "Global Digital Bank",
            BankBranch = "Main Branch",
            IfscCode = "GDB0000001",
            IsActive = true,
            ActivatedDate = DateTime.UtcNow
        };
        dbContext.Accounts.Add(currentAccount);
        dbContext.SaveChanges();

        var currentDetails = new AccountsService.Infrastructure.Data.Entities.CurrentAccountDetailsEntity
        {
            AccountNumber = 1001,
            CompanyName = "Admin Tech Corp",
            RegistrationNo = regNo,
            Website = "admintech.com"
        };
        dbContext.CurrentAccountDetails.Add(currentDetails);
        dbContext.SaveChanges();

        NLog.LogManager.GetCurrentClassLogger().Info("Seeded 2 default accounts (Savings #1000, Current #1001).");
    }
}

// Aadhaar protection upgrade: migrate any rows still in the pre-upgrade format (AES-CBC / plaintext,
// legacy blind index) to AES-GCM + the dedicated blind-index key. Idempotent; runs in every environment
// because production databases predate the key split too. Failure degrades reads of unmigrated rows
// to "masked-empty" (fail-closed), so it is logged loudly but does not block startup.
if (settings.AadhaarCryptoUpgradeOnStartup)
{
    try
    {
        using var _cryptoScope = app.Services.CreateScope();
        await AccountsService.Infrastructure.Data.AadhaarCryptoUpgrade.RunAsync(
            _cryptoScope.ServiceProvider.GetRequiredService<AppDbContext>(),
            _cryptoScope.ServiceProvider.GetRequiredService<AccountsService.Utils.EncryptionManager>(),
            _cryptoScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AadhaarCryptoUpgrade"));
    }
    catch (Exception _cex)
    {
        NLog.LogManager.GetCurrentClassLogger().Error(_cex, "Aadhaar crypto upgrade failed; unmigrated rows will read as masked-empty until it succeeds");
    }
}

// When the ADO.NET path is active on SQL Server, (re)create the usp_AccountSummary stored
// procedure that GetSummaryAsync calls. Idempotent (CREATE OR ALTER) and safe on every startup.
if (string.Equals(settings.DataAccess, "AdoNet", StringComparison.OrdinalIgnoreCase))
{
    var _adoProvider = settings.DatabaseProvider.ToLowerInvariant();
    if (_adoProvider == "sqlserver" || _adoProvider == "mssql")
    {
        try
        {
            using var _spScope = app.Services.CreateScope();
            var _spLogger = _spScope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger<AdoNetAccountRepository>();
            await AdoNetAccountRepository.EnsureStoredProceduresAsync(settings, _spLogger);
        }
        catch (Exception _spex)
        {
            NLog.LogManager.GetCurrentClassLogger().Warn(_spex, "Failed to ensure usp_AccountSummary stored procedure");
        }
    }
}

app.Run();






// Exposes the generated entry-point class to WebApplicationFactory<Program> in the integration tests.
public partial class Program { }

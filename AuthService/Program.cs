using Microsoft.EntityFrameworkCore;
using NLog.Web;
using AuthService.Config;
using AuthService.Infrastructure.Data;
using AuthService.Infrastructure.Repositories;
using AuthService.Integration;
using AuthService.Security;
using AuthService.Services;
using AuthService.Domain.Ports;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Load Settings
var settings = new Settings();
builder.Configuration.Bind(settings);
// Honour the DATA_ACCESS environment variable as an override for the data-access strategy
// ("EfCore" default | "AdoNet"). Config binding maps top-level keys, so read the underscored
// env var name explicitly here.
var dataAccessEnv = Environment.GetEnvironmentVariable("DATA_ACCESS");
if (!string.IsNullOrWhiteSpace(dataAccessEnv))
    settings.DataAccess = dataAccessEnv;

// Fail-closed secret validation — runs in EVERY environment (see Gdb.Common.Security.SecureConfigGuard).
// With AllowInsecureDefaults=false (the default), empty/default secrets refuse to start.
// The dev opt-in is never valid in Production, regardless of what config says.
Gdb.Common.Security.SecureConfigGuard.AssertProductionSafe(builder.Environment.IsProduction(), settings.AllowInsecureDefaults);
Gdb.Common.Security.SecureConfigGuard.Assert(settings.AllowInsecureDefaults,
    ("JwtSecretKey", settings.JwtSecretKey),
    ("InternalApiKey", settings.InternalApiKey));
if (settings.AllowInsecureDefaults)
{
    settings.JwtSecretKey = Gdb.Common.Security.SecureConfigGuard.OrDevDefault(settings.JwtSecretKey, Gdb.Common.Security.SecureConfigGuard.DevJwtSecretKey);
    settings.InternalApiKey = Gdb.Common.Security.SecureConfigGuard.OrDevDefault(settings.InternalApiKey, Gdb.Common.Security.SecureConfigGuard.DevInternalApiKey);
}

// This service MINTS tokens: the HMAC key must carry ≥256 bits even when dev defaults are in play.
if (!string.Equals(settings.JwtAlgorithm, "RS256", StringComparison.OrdinalIgnoreCase))
    Gdb.Common.Security.SecureConfigGuard.AssertMinimumKeyLength("JwtSecretKey", settings.JwtSecretKey);
// Honour ASPNETCORE_URLS when the host/orchestrator sets it (containers, CI, the OpenAPI gate);
// fall back to the configured Host/Port only for plain local runs.
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    builder.WebHost.UseUrls($"http://{settings.Host}:{settings.Port}");
builder.Services.AddSingleton(settings);
// /live (process up) and /ready (database reachable) via the framework health-check subsystem.
Gdb.Common.Health.GdbHealthExtensions.AddDatabase<AppDbContext>(Gdb.Common.Health.GdbHealthExtensions.AddGdbHealthChecks(builder.Services));

// Setup Logging
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// Database Context
builder.Services.AddDbContextPool<AppDbContext>(options =>
{
    var provider = settings.DatabaseProvider.ToLower();
    if (provider == "sqlite")
    {
        options.UseSqlite(string.IsNullOrEmpty(settings.DatabaseUrl) ? "Data Source=auth.db" : settings.DatabaseUrl, o => o.CommandTimeout(30)).UseSnakeCaseNamingConvention();
    }
    else if (provider == "postgres")
    {
        var connStr = string.IsNullOrEmpty(settings.DatabaseUrl) 
            ? $"Host={settings.DatabaseHost};Port={settings.DatabasePort};Database={settings.DatabaseName};Username={settings.DatabaseUser};Password={settings.DatabasePassword}" 
            : settings.DatabaseUrl;
        options.UseNpgsql(connStr, o => o.EnableRetryOnFailure(3).CommandTimeout(30)).UseSnakeCaseNamingConvention();
    }
    else if (provider == "mysql")
    {
        var connStr = string.IsNullOrEmpty(settings.DatabaseUrl)
            ? $"Server={settings.DatabaseHost};Port={settings.DatabasePort};Database={settings.DatabaseName};User={settings.DatabaseUser};Password={settings.DatabasePassword}"
            : settings.DatabaseUrl;
        options.UseMySQL(connStr, o => o.CommandTimeout(30)).UseSnakeCaseNamingConvention();
    }
    else if (provider == "sqlserver" || provider == "mssql")
    {
        var connStr = string.IsNullOrEmpty(settings.DatabaseUrl) 
            ? $"Server={settings.DatabaseHost},{settings.DatabasePort};Database={settings.DatabaseName};User Id={settings.DatabaseUser};Password={settings.DatabasePassword};TrustServerCertificate=True;{Gdb.Common.Data.ConnectionPooling.SqlServer}" 
            : settings.DatabaseUrl;
        options.UseSqlServer(connStr, o => o.EnableRetryOnFailure(3).CommandTimeout(30)).UseSnakeCaseNamingConvention();
    }
    else
    {
        options.UseInMemoryDatabase("gdb_auth_db");
    }
});

// Dependency Injection
// Data-access toggle: AdoNet (SQL Server only) or EF Core (default). AddDbContext above stays
// registered regardless; when AdoNet is selected the EF context is simply unused for repository
// ops and the AdoNet unit-of-work treats CommitAsync() as a no-op (each write persists eagerly).
if (string.Equals(settings.DataAccess, "AdoNet", StringComparison.OrdinalIgnoreCase))
{
    var _adoProvider = settings.DatabaseProvider.ToLowerInvariant();
    if (_adoProvider != "sqlserver" && _adoProvider != "mssql")
        throw new InvalidOperationException(
            $"DataAccess=AdoNet is supported only with the 'sqlserver' provider; current provider is '{settings.DatabaseProvider}'.");
    builder.Services.AddScoped<IAuthTokenRepository, AdoNetAuthTokenRepository>();
    builder.Services.AddScoped<IAuthAuditRepository, AdoNetAuthAuditRepository>();
    builder.Services.AddScoped<IUnitOfWork, AdoNetUnitOfWork>();
}
else
{
    builder.Services.AddScoped<IAuthTokenRepository, AuthTokenRepository>();
    builder.Services.AddScoped<IAuthAuditRepository, AuthAuditRepository>();
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
}

builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddSingleton<JwtUtil>();
builder.Services.AddScoped<AuthenticationService>();

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

// HTTP Client with Polly Circuit Breaker
builder.Services.AddHttpClient<IUserServicePort, UserServiceClient>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(settings.UserServiceTimeout);
    })
    .AddPolicyHandler(Gdb.Common.Integration.PollyPolicies.CreateSelector());

builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers(options => options.Filters.Add<Gdb.Common.Filters.FastApiValidationFilter>());
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
builder.Services.AddEndpointsApiExplorer();
Gdb.Common.Swagger.GdbSwaggerExtensions.AddGdbSwagger(builder.Services, "Auth Service API");

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        b =>
        {
            var origins = settings.CorsAllowedOrigins.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            b.WithOrigins(origins)
             .AllowAnyMethod()
             .AllowAnyHeader()
             .AllowCredentials();
        });
});

Gdb.Common.Middleware.GdbMiddlewareExtensions.AddGdbCrossCuttingMiddleware(builder.Services);
Gdb.Common.Observability.GdbOpenTelemetryExtensions.AddGdbOpenTelemetry(builder.Services, "auth-service");
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30)); // drain in-flight requests before the container is killed
Gdb.Common.Caching.GdbDistributedCacheExtensions.AddGdbDistributedCache(builder.Services, builder.Configuration, builder.Environment, "auth-service");

var app = builder.Build();

// Cross-cutting: correlation/trace id + security response headers (outermost).
Gdb.Common.Middleware.GdbMiddlewareExtensions.UseGdbCrossCuttingMiddleware(app);


// Served only in Development (or EnableSwagger=true) — never disclosed by default in Production.
Gdb.Common.Swagger.GdbSwaggerExtensions.UseGdbSwagger(app, "Auth Service API");

app.UseCors("AllowSpecificOrigins");

app.MapControllers();
Gdb.Common.Middleware.MetricsEndpointExtensions.MapGdbMetrics(app);
// /live and /ready (database-probing) — this service previously exposed neither.
Gdb.Common.Health.GdbHealthExtensions.MapGdbHealthEndpoints(app);

app.MapGet("/", () => new {
    service = settings.AppName,
    version = settings.AppVersion
}).ExcludeFromDescription();

app.MapGet("/health", () => new
{
    status = "ok",
    service = "auth-service"
}).ExcludeFromDescription();

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
    // sqlite -> versioned EF Core migrations; inmemory/other RDBMS -> EnsureCreated
    // (TODO(P3-1): per-provider migration sets for postgres/mysql/sqlserver).
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
}

// Housekeeping: drop access/refresh tokens that expired more than a day ago (they can never validate again).
if (!app.Environment.IsProduction() || settings.MigrateOnStartup)
{
    try
    {
        using var _purgeScope = app.Services.CreateScope();
        var _purged = await _purgeScope.ServiceProvider.GetRequiredService<IAuthTokenRepository>()
            .PurgeExpiredAsync(DateTime.UtcNow.AddDays(-1));
        if (_purged > 0)
            NLog.LogManager.GetCurrentClassLogger().Info($"Purged {_purged} expired auth token(s).");
    }
    catch (Exception _pex)
    {
        NLog.LogManager.GetCurrentClassLogger().Warn(_pex, "Expired-token purge skipped");
    }
}

// When the ADO.NET path is active on SQL Server, (re)create the usp_GetAuthTokenByJti stored
// procedure that GetTokenAsync calls. Idempotent (CREATE OR ALTER) and safe on every startup.
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
                .CreateLogger<AdoNetAuthTokenRepository>();
            await AdoNetAuthTokenRepository.EnsureStoredProceduresAsync(settings, _spLogger);
        }
        catch (Exception _spex)
        {
            NLog.LogManager.GetCurrentClassLogger().Warn(_spex, "Failed to ensure usp_GetAuthTokenByJti stored procedure");
        }
    }
}

app.Run();



// Exposes the generated entry-point class to WebApplicationFactory<Program> in the integration tests.
public partial class Program { }

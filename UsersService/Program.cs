using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NLog.Web;
using UsersService.Config;
using UsersService.Infrastructure.Data;
using UsersService.Infrastructure.Repositories;
using UsersService.Services;
using Gdb.Common.Security;

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
// The dev opt-in is never valid in Production, regardless of what config says.
SecureConfigGuard.AssertProductionSafe(builder.Environment.IsProduction(), settings.AllowInsecureDefaults);
SecureConfigGuard.Assert(settings.AllowInsecureDefaults,
    ("JwtSecretKey", settings.JwtSecretKey),
    ("InternalApiKey", settings.InternalApiKey));
if (settings.AllowInsecureDefaults)
{
    settings.JwtSecretKey = SecureConfigGuard.OrDevDefault(settings.JwtSecretKey, SecureConfigGuard.DevJwtSecretKey);
    settings.InternalApiKey = SecureConfigGuard.OrDevDefault(settings.InternalApiKey, SecureConfigGuard.DevInternalApiKey);
}

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
        options.UseSqlite(string.IsNullOrEmpty(settings.DatabaseUrl) ? "Data Source=users.db" : settings.DatabaseUrl, o => o.CommandTimeout(30)).UseSnakeCaseNamingConvention();
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
        options.UseInMemoryDatabase("gdb_users_db");
    }
});

// Dependency Injection
// Data-access toggle: AdoNet (SQL Server only) or EF Core (default). AddDbContext above stays
// registered regardless; when AdoNet is selected the EF context is simply unused for repository ops.
if (string.Equals(settings.DataAccess, "AdoNet", StringComparison.OrdinalIgnoreCase))
{
    var _adoProvider = settings.DatabaseProvider.ToLowerInvariant();
    if (_adoProvider != "sqlserver" && _adoProvider != "mssql")
        throw new InvalidOperationException(
            $"DataAccess=AdoNet is supported only with the 'sqlserver' provider; current provider is '{settings.DatabaseProvider}'.");
    builder.Services.AddScoped<IUserRepository, AdoNetUserRepository>();
    builder.Services.AddScoped<IAuditRepository, AdoNetAuditRepository>();
}
else
{
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IAuditRepository, AuditRepository>();
}
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
// CONCEPT: AutoMapper registration - scans the assembly for Profile subclasses (UserMappingProfile).
builder.Services.AddAutoMapper(cfg => { }, typeof(UsersService.Mapping.UserMappingProfile).Assembly);
builder.Services.AddScoped<UserService>();
builder.Services.AddHttpClient();


if (settings.ServiceDiscoveryEnabled)
{
    Gdb.Common.Discovery.ServiceDiscoveryExtensions.AddGdbServiceDiscovery(builder.Services, settings.DiscoveryName, settings.InstanceUrl, settings.RegistryUrl);
}

// Authentication
if (!string.Equals(settings.JwtAlgorithm, "RS256", StringComparison.OrdinalIgnoreCase))
    SecureConfigGuard.AssertMinimumKeyLength("JwtSecretKey", settings.JwtSecretKey);
builder.Services.AddGdbJwtAuthentication(settings.JwtSecretKey, settings.JwtAlgorithm, settings.JwtPublicKey,
    settings.JwtIssuer, settings.JwtAudience);

// Controllers and Swagger
builder.Services.AddControllers(options => options.Filters.Add<Gdb.Common.Filters.FastApiValidationFilter>());
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
builder.Services.AddEndpointsApiExplorer();
Gdb.Common.Swagger.GdbSwaggerExtensions.AddGdbSwagger(builder.Services, settings.Title, settings.ServiceVersion, settings.Description);

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        b =>
        {
            var origins = settings.CorsOrigins.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            b.WithOrigins(origins)
             .AllowAnyMethod()
             .AllowAnyHeader()
             .AllowCredentials();
        });
});

// Use the guard-RESOLVED key (dev default in dev, injected real key in prod), NOT the raw
// config value. appsettings has InternalApiKey="" under the fail-closed model, so reading raw
// config gave the [InternalApi] filter an empty expected-key → it rejected every internal call
// with 401 ("Internal API key is not configured"), which AuthService mis-reported as "Invalid
// credentials" — blocking ALL logins.
builder.Services.AddSingleton<Func<string>>(() => settings.InternalApiKey);
Gdb.Common.Middleware.GdbMiddlewareExtensions.AddGdbCrossCuttingMiddleware(builder.Services);
Gdb.Common.Observability.GdbOpenTelemetryExtensions.AddGdbOpenTelemetry(builder.Services, "users-service");
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30)); // drain in-flight requests before the container is killed

var app = builder.Build();

// Cross-cutting: correlation/trace id + security response headers (outermost).
Gdb.Common.Middleware.GdbMiddlewareExtensions.UseGdbCrossCuttingMiddleware(app);

app.UsePathBase(settings.ApiPrefix);

// Served only in Development (or EnableSwagger=true) — never disclosed by default in Production.
Gdb.Common.Swagger.GdbSwaggerExtensions.UseGdbSwagger(app, settings.Title, settings.ApiPrefix);

app.UseCors("AllowSpecificOrigins");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
Gdb.Common.Middleware.MetricsEndpointExtensions.MapGdbMetrics(app);

app.MapGet("/", () => new {
    service = settings.ServiceName,
    version = settings.ServiceVersion,
    docs = $"{settings.ApiPrefix}/docs",
    health = $"{settings.ApiPrefix}/health"
}).ExcludeFromDescription().AllowAnonymous(); // service banner: no data, no token required

// Probes must answer without a token (deny-by-default fallback policy applies to every other endpoint).
app.MapGet("/api/v1/health", () => new
{
    status = "healthy",
    service = settings.ServiceName,
    version = settings.ServiceVersion
}).AllowAnonymous();

// /ready now actually probes the database; /live only reports that the process serves HTTP.
Gdb.Common.Health.GdbHealthExtensions.MapGdbHealthEndpoints(app);

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

    var userSvc = scope.ServiceProvider.GetRequiredService<UserService>();
    if (!dbContext.Users.Any())
    {
        NLog.LogManager.GetCurrentClassLogger().Info("Seeding default users...");
        var seedUsers = new[]
        {
            ("Admin User", "admin", "ADMIN"),
            ("Sarah Johnson", "sarah.admin", "ADMIN"),
            ("John Doe", "john.doe", "TELLER"),
            ("Emily Davis", "emily.davis", "TELLER"),
            ("Teller User", "teller", "TELLER"),
            ("Manager User", "manager.manager", "MANAGER"),
            ("Jane Smith", "jane.smith", "MANAGER"),
            ("Michael Brown", "michael.brown", "MANAGER"),
            ("Alice Wilson", "alice.wilson", "MANAGER"),
            ("Bob Taylor", "bob.taylor", "MANAGER")
        };

        foreach (var (name, login, role) in seedUsers)
        {
            userSvc.AddUserAsync(new UsersService.DTOs.AddUserRequest
            {
                Username = name,
                LoginId = login,
                Password = "Welcome@1",
                Role = role
            }).GetAwaiter().GetResult();
        }
        NLog.LogManager.GetCurrentClassLogger().Info($"Seeded {seedUsers.Length} default users.");
    }
}

// When the ADO.NET path is active on SQL Server, (re)create the usp_UserList stored procedure
// that GetAllUsersAsync calls. Idempotent (CREATE OR ALTER) and safe on every startup.
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
                .CreateLogger<AdoNetUserRepository>();
            await AdoNetUserRepository.EnsureStoredProceduresAsync(settings, _spLogger);
        }
        catch (Exception _spex)
        {
            NLog.LogManager.GetCurrentClassLogger().Warn(_spex, "Failed to ensure usp_UserList stored procedure");
        }
    }
}

app.Run();





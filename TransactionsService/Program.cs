using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NLog.Web;
using TransactionsService.Config;
using TransactionsService.Infrastructure.Data;
using TransactionsService.Infrastructure.Repositories;
using TransactionsService.Integration;
using TransactionsService.Services;
using Gdb.Common.Security;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Load Settings
var settings = new Settings();
builder.Configuration.Bind(settings);
// Honour the DATA_ACCESS environment variable as an override for the data-access strategy
// ("EfCore" default | "AdoNet"). Config binding maps top-level keys, so read the underscored
// env var name explicitly here (after binding so it wins over appsettings).
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
        options.UseSqlite(string.IsNullOrEmpty(settings.DatabaseUrl) ? "Data Source=transactions.db" : settings.DatabaseUrl, o => o.CommandTimeout(30)).UseSnakeCaseNamingConvention();
    }
    else if (provider == "postgres" || provider == "supabase")
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
        options.UseInMemoryDatabase("gdb_transactions_db");
    }
});

// Dependency Injection
// Data-access toggle: AdoNet (SQL Server only) or EF Core (default). AddDbContext above stays
// registered regardless; when AdoNet is selected the EF context is simply unused for repository ops
// (UnitOfWork receives the AdoNet repos, so the whole service runs on hand-written SQL).
if (string.Equals(settings.DataAccess, "AdoNet", StringComparison.OrdinalIgnoreCase))
{
    var _adoProvider = settings.DatabaseProvider.ToLowerInvariant();
    if (_adoProvider != "sqlserver" && _adoProvider != "mssql")
        throw new InvalidOperationException(
            $"DataAccess=AdoNet is supported only with the 'sqlserver' provider; current provider is '{settings.DatabaseProvider}'.");
    builder.Services.AddScoped<ITransactionRepository, AdoNetTransactionRepository>();
    builder.Services.AddScoped<ITransactionLogRepository, AdoNetTransactionLogRepository>();
    builder.Services.AddScoped<ITransferLimitRepository, AdoNetTransferLimitRepository>();
    builder.Services.AddScoped<IIdempotencyRepository, AdoNetIdempotencyRepository>();
    builder.Services.AddScoped<IUnitOfWork, AdoNetUnitOfWork>();
}
else
{
    builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
    builder.Services.AddScoped<ITransactionLogRepository, TransactionLogRepository>();
    builder.Services.AddScoped<ITransferLimitRepository, TransferLimitRepository>();
    builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
}

builder.Services.AddScoped<IDepositService, DepositService>();
builder.Services.AddScoped<IWithdrawService, WithdrawService>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<ITransactionLogService, TransactionLogService>();
builder.Services.AddScoped<TransferLimitService>();

// HTTP Clients with Polly
builder.Services.AddHttpClient();

// Caching
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<TransactionsService.Utils.CacheInvalidator>();

// Ledger audit file is written off the request path by one background writer.
builder.Services.AddSingleton<TransactionsService.Infrastructure.Audit.TransactionAuditFileWriter>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TransactionsService.Infrastructure.Audit.TransactionAuditFileWriter>());


// Each client owns ONE breaker; transient-error retry applies only to idempotent (GET) requests so a
// lost response to POST /debit or /credit can never be replayed into a double movement of money.
var accountsPolicy = Gdb.Common.Integration.PollyPolicies.CreateSelector();
var paymentPolicy = Gdb.Common.Integration.PollyPolicies.CreateSelector();
var notificationPolicy = Gdb.Common.Integration.PollyPolicies.CreateSelector();
builder.Services.AddHttpClient<AccountServiceClient>(c => c.Timeout = TimeSpan.FromSeconds(10)).AddPolicyHandler(request => accountsPolicy(request));
builder.Services.AddHttpClient<PaymentGatewayClient>(c => c.Timeout = TimeSpan.FromSeconds(10)).AddPolicyHandler(request => paymentPolicy(request));
builder.Services.AddHttpClient<NotificationClient>(c => c.Timeout = TimeSpan.FromSeconds(10)).AddPolicyHandler(request => notificationPolicy(request));

// Reconciliation: sweeps transfers stuck in PENDING / COMPENSATION_FAILED and orphaned idempotency reservations.
builder.Services.AddHostedService<TransactionsService.Services.TransferReconciliationService>();

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

// Authentication
if (!string.Equals(settings.JwtAlgorithm, "RS256", StringComparison.OrdinalIgnoreCase))
    SecureConfigGuard.AssertMinimumKeyLength("JwtSecretKey", settings.JwtSecretKey);
builder.Services.AddGdbJwtAuthentication(settings.JwtSecretKey, settings.JwtAlgorithm, settings.JwtPublicKey,
    settings.JwtIssuer, settings.JwtAudience);

// Controllers and Swagger
builder.Services.AddControllers(options => options.Filters.Add<Gdb.Common.Filters.FastApiValidationFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    });
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options => 
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower);
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
builder.Services.AddEndpointsApiExplorer();
Gdb.Common.Swagger.GdbSwaggerExtensions.AddGdbSwagger(builder.Services, settings.Title, settings.AppVersion, settings.Description);

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
Gdb.Common.Observability.GdbOpenTelemetryExtensions.AddGdbOpenTelemetry(builder.Services, "transactions-service");
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30)); // drain in-flight requests before the container is killed
Gdb.Common.Caching.GdbDistributedCacheExtensions.AddGdbDistributedCache(builder.Services, builder.Configuration, builder.Environment, "transactions-service");

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
    service = settings.Title,
    version = settings.AppVersion,
    description = settings.Description,
    status = "running",
    docs = $"{settings.ApiPrefix}/docs",
    health = $"{settings.ApiPrefix}/health"
}).ExcludeFromDescription().AllowAnonymous(); // service banner: no data, no token required

// Probes must answer without a token (deny-by-default fallback policy applies to every other endpoint).
app.MapGet("/api/v1/health", () => new
{
    status = "healthy",
    service = settings.AppName,
    version = settings.AppVersion
}).AllowAnonymous();

// /ready now actually probes the database (the old handler reported database="connected" as a constant).
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
    
    // transfer_limits are seeded by the model itself (HasData in AppDbContext) - nothing to do here.

    if (!dbContext.TransactionLogs.Any())
    {
        NLog.LogManager.GetCurrentClassLogger().Info("Seeding sample transaction logs...");
        
        var depositLog = new TransactionsService.Infrastructure.Data.DepositLogEntity
        {
            AccountId = 1001,
            Amount = 60000.00m,
            TransactionType = TransactionsService.Domain.Models.TransactionType.DEPOSIT,
            ReferenceId = null,
            Description = "Initial deposit",
            BalanceAfter = 60000.00m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        
        var transferLogDebit = new TransactionsService.Infrastructure.Data.TransferLegEntity
        {
            AccountId = 1002,
            Amount = 10000.00m,
            TransactionType = TransactionsService.Domain.Models.TransactionType.TRANSFER,
            ReferenceId = "1",
            Description = "Fund Transfer to 1001",
            BalanceAfter = -10000.00m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-2)
        };

        var transferLogCredit = new TransactionsService.Infrastructure.Data.TransferLegEntity
        {
            AccountId = 1001,
            Amount = 10000.00m,
            TransactionType = TransactionsService.Domain.Models.TransactionType.TRANSFER,
            ReferenceId = "1",
            Description = "Fund Transfer from 1002",
            BalanceAfter = 70000.00m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-2)
        };

        var transfer = new TransactionsService.Infrastructure.Data.FundTransferEntity
        {
            SourceAccountId = 1002,
            DestinationAccountId = 1001,
            Amount = 10000.00m,
            TransferMode = TransactionsService.Domain.Models.TransferMode.IMPS,
            Status = "COMPLETED",
            CreatedAt = DateTime.UtcNow.AddMinutes(-2)
        };

        dbContext.FundTransfers.Add(transfer);
        dbContext.SaveChanges(); // the transfer needs its id before the legs can reference it

        // The two TRANSFER legs belong to the transfer (one-to-many FK); the deposit stands alone.
        transferLogDebit.TransferId = transfer.Id;
        transferLogCredit.TransferId = transfer.Id;

        // One bulk write through the repository: EF batches with AutoDetectChanges off; the ADO.NET path uses SqlBulkCopy.
        scope.ServiceProvider.GetRequiredService<ITransactionLogRepository>()
            .BulkInsertAsync(new TransactionsService.Infrastructure.Data.TransactionLogEntity[] { depositLog, transferLogDebit, transferLogCredit })
            .GetAwaiter().GetResult();
    }
}

// When the ADO.NET path is active on SQL Server, (re)create the usp_TransferDailyStats stored
// procedure that GetDailyUsedAmountAsync calls. Idempotent (CREATE OR ALTER) and safe on every startup.
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
                .CreateLogger("AdoNetStoredProcedures");
            await TransactionsService.Infrastructure.Repositories.AdoNetSupport.EnsureStoredProceduresAsync(settings, _spLogger);
        }
        catch (Exception _spex)
        {
            NLog.LogManager.GetCurrentClassLogger().Warn(_spex, "Failed to ensure usp_TransferDailyStats stored procedure");
        }
    }
}

app.Run();



// Exposes the generated entry-point class to WebApplicationFactory<Program> in the integration tests.
public partial class Program { }

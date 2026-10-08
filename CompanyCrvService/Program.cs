using NLog.Web;
using CompanyCrvService.Config;
using CompanyCrvService.Services;

var builder = WebApplication.CreateBuilder(args);

// Load Settings
var settings = new Settings();
builder.Configuration.Bind(settings);
// Honour ASPNETCORE_URLS when the host/orchestrator sets it (containers, CI, the OpenAPI gate);
// fall back to the configured Host/Port only for plain local runs.
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    builder.WebHost.UseUrls($"http://{settings.Host}:{settings.Port}");
builder.Services.AddSingleton(settings);

// Setup Logging
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// Dependency Injection
builder.Services.AddSingleton<CompanyVerificationService>();
builder.Services.AddHttpClient();

if (settings.ServiceDiscoveryEnabled)
{
    Gdb.Common.Discovery.ServiceDiscoveryExtensions.AddGdbServiceDiscovery(builder.Services, settings.DiscoveryName, settings.InstanceUrl, settings.RegistryUrl);
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
Gdb.Common.Swagger.GdbSwaggerExtensions.AddGdbSwagger(builder.Services, "Company CRV Service API");

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

// Fail-closed internal-key validation (see Gdb.Common.Security.SecureConfigGuard): the key is
// never baked into code; a dev fallback applies only under the explicit, non-Production opt-in.
var internalApiKey = builder.Configuration["InternalApiKey"] ?? string.Empty;
var allowInsecureDefaults = builder.Configuration.GetValue<bool>("AllowInsecureDefaults");
Gdb.Common.Security.SecureConfigGuard.AssertProductionSafe(builder.Environment.IsProduction(), allowInsecureDefaults);
Gdb.Common.Security.SecureConfigGuard.Assert(allowInsecureDefaults, ("InternalApiKey", internalApiKey));
if (allowInsecureDefaults)
    internalApiKey = Gdb.Common.Security.SecureConfigGuard.OrDevDefault(internalApiKey, Gdb.Common.Security.SecureConfigGuard.DevInternalApiKey);
builder.Services.AddSingleton<Func<string>>(() => internalApiKey);
Gdb.Common.Middleware.GdbMiddlewareExtensions.AddGdbCrossCuttingMiddleware(builder.Services);
Gdb.Common.Observability.GdbOpenTelemetryExtensions.AddGdbOpenTelemetry(builder.Services, "company-service");
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30)); // drain in-flight requests before the container is killed

var app = builder.Build();

// Cross-cutting: correlation/trace id + security response headers (outermost).
Gdb.Common.Middleware.GdbMiddlewareExtensions.UseGdbCrossCuttingMiddleware(app);

// Served only in Development (or EnableSwagger=true) — never disclosed by default in Production.
Gdb.Common.Swagger.GdbSwaggerExtensions.UseGdbSwagger(app, "Company CRV Service API");

app.UseCors("AllowSpecificOrigins");

app.MapControllers();
Gdb.Common.Middleware.MetricsEndpointExtensions.MapGdbMetrics(app);

app.MapGet("/", () => new {
    service = settings.AppName,
    version = settings.AppVersion
}).ExcludeFromDescription();

// Startup Log (similar to FastAPI startup event)
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("{ServiceName} starting on port {Port}", settings.AppName, settings.Port);
logger.LogInformation("Valid company records loaded: {Count}", CompanyVerificationService.ValidRegistrationNumbers.Count);

app.Lifetime.ApplicationStopping.Register(() => 
{
    logger.LogInformation("{ServiceName} shutting down", settings.AppName);
});

app.Run();




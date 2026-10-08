using Microsoft.OpenApi.Models;
using NLog.Web;
using CentralPaymentGatewayService.Config;

using CentralPaymentGatewayService.Services;
using Microsoft.AspNetCore.Mvc;
using Gdb.Common.Middleware;

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
builder.Services.AddSingleton<PaymentGatewayService>();
builder.Services.AddHttpClient();

if (settings.ServiceDiscoveryEnabled)
{
    Gdb.Common.Discovery.ServiceDiscoveryExtensions.AddGdbServiceDiscovery(builder.Services, settings.DiscoveryName, settings.InstanceUrl, settings.RegistryUrl);
}

// Validation behavior mapping
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
        var message = string.Join("; ", errors);
        return new UnprocessableEntityObjectResult(new { detail = message });
    };
});

// Controllers and Swagger
builder.Services.AddControllers(options => options.Filters.Add<Gdb.Common.Filters.FastApiValidationFilter>());
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
builder.Services.AddEndpointsApiExplorer();
Gdb.Common.Swagger.GdbSwaggerExtensions.AddGdbSwagger(builder.Services, settings.AppName, settings.AppVersion,
    "Simulated Third-Party Payment Gateway for GDB Ecosystem", internalApiKey: true);

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
Gdb.Common.Observability.GdbOpenTelemetryExtensions.AddGdbOpenTelemetry(builder.Services, "payment-gateway-service");
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30)); // drain in-flight requests before the container is killed

var app = builder.Build();

// Cross-cutting: correlation/trace id + security response headers (outermost).
Gdb.Common.Middleware.GdbMiddlewareExtensions.UseGdbCrossCuttingMiddleware(app);


// Served only in Development (or EnableSwagger=true) — never disclosed by default in Production.
Gdb.Common.Swagger.GdbSwaggerExtensions.UseGdbSwagger(app, settings.AppName);

app.UseCors("AllowSpecificOrigins");

app.MapControllers();
Gdb.Common.Middleware.MetricsEndpointExtensions.MapGdbMetrics(app);

app.MapGet("/", () => new {
    service = settings.AppName,
    version = settings.AppVersion,
    docs = "/docs",
    health = "/health"
}).ExcludeFromDescription();

app.MapGet("/health", () => new
{
    status = "active",
    service = settings.AppName
});

// /live and /ready are served by HealthController (like the other mock services). Mapping them here as
// well made every request to /live an AmbiguousMatchException and broke OpenAPI generation
// ("Conflicting method/path combination GET ready"), which is why this service's contract snapshot
// had silently degraded to an empty document.

app.Run();







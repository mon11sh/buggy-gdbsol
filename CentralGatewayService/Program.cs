using NLog.Web;
using CentralGatewayService.Config;
using CentralGatewayService.Resolvers;
using CentralGatewayService.Middleware;
using CentralGatewayService.Proxy;

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

// Dependency Injection for Resolvers
builder.Services.AddSingleton<StaticResolver>();
builder.Services.AddHttpClient("RegistryClient");

if (settings.ServiceDiscoveryEnabled)
{
    builder.Services.AddSingleton<IServiceResolver>(sp => 
        new RegistryResolver(
            settings,
            sp.GetRequiredService<StaticResolver>(),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<RegistryResolver>>()
        )
    );
}
else
{
    builder.Services.AddSingleton<IServiceResolver>(sp => sp.GetRequiredService<StaticResolver>());
}

// HTTP Client for Proxy
builder.Services.AddHttpClient("GatewayClient")
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));

builder.Services.AddControllers(options => options.Filters.Add<Gdb.Common.Filters.FastApiValidationFilter>());
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
builder.Services.AddTransient<Gdb.Common.Middleware.CorrelationIdMiddleware>();
builder.Services.AddSingleton<Gdb.Common.Middleware.MetricsRegistry>();
builder.Services.AddTransient<Gdb.Common.Middleware.MetricsMiddleware>();
Gdb.Common.Observability.GdbOpenTelemetryExtensions.AddGdbOpenTelemetry(builder.Services, "api-gateway");
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));
builder.Services.AddEndpointsApiExplorer();
Gdb.Common.Swagger.GdbSwaggerExtensions.AddGdbSwagger(builder.Services, "GDB API Gateway", "1.0.0",
    configure: c => c.DocumentFilter<CentralGatewayService.Config.ProxyDocumentFilter>());

// CORS
var corsOrigins = settings.CorsAllowedOrigins.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
var allowCredentials = !corsOrigins.Contains("*");

builder.Services.AddCors(options =>
{
    options.AddPolicy("GatewayCorsPolicy", b =>
    {
        if (!allowCredentials)
        {
            b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
        else
        {
            b.WithOrigins(corsOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
        }
    });
});

// The gateway uses the internal key to talk to the registry, so it is guarded like every backend.
var internalApiKey = builder.Configuration["InternalApiKey"] ?? string.Empty;
var allowInsecureDefaults = builder.Configuration.GetValue<bool>("AllowInsecureDefaults");
Gdb.Common.Security.SecureConfigGuard.AssertProductionSafe(builder.Environment.IsProduction(), allowInsecureDefaults);
Gdb.Common.Security.SecureConfigGuard.Assert(allowInsecureDefaults, ("InternalApiKey", internalApiKey));
if (allowInsecureDefaults)
    internalApiKey = Gdb.Common.Security.SecureConfigGuard.OrDevDefault(internalApiKey, Gdb.Common.Security.SecureConfigGuard.DevInternalApiKey);
builder.Services.AddSingleton<Func<string>>(() => internalApiKey);
if (settings.TrustForwardedHeaders)
{
    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
        o.KnownIPNetworks.Clear(); // the ingress address is deployment-specific; trust is opt-in via TrustForwardedHeaders
        o.KnownProxies.Clear();
    });
}

var app = builder.Build();

if (settings.TrustForwardedHeaders)
    app.UseForwardedHeaders(); // first: everything after it (rate limiting, logging) sees the real client

app.UseCors("GatewayCorsPolicy");

app.UseMiddleware<Gdb.Common.Middleware.CorrelationIdMiddleware>();
app.UseMiddleware<Gdb.Common.Middleware.MetricsMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();

// Served only in Development (or EnableSwagger=true) — never disclosed by default in Production.
Gdb.Common.Swagger.GdbSwaggerExtensions.UseGdbSwagger(app, "GDB API Gateway");

app.MapControllers();
Gdb.Common.Middleware.MetricsEndpointExtensions.MapGdbMetrics(app);

app.MapGet("/", () => new {
    service = settings.AppName,
    version = settings.AppVersion
}).ExcludeFromDescription();

// Catch-All Proxy Route
app.MapFallback(async context => 
{
    var resolver = context.RequestServices.GetRequiredService<IServiceResolver>();
    var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
    await GatewayHandler.HandleProxyRequest(context, resolver, httpClientFactory);
}).ExcludeFromDescription();

app.Run();




using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Gdb.Common.Health;

/// <summary>
/// Probes the service's database with a real connection attempt. Registered with the
/// <see cref="GdbHealthExtensions.ReadyTag"/> so it gates <c>/ready</c> but not <c>/live</c>.
/// </summary>
public sealed class DbContextHealthCheck<TContext> : IHealthCheck where TContext : DbContext
{
    private readonly TContext _db;

    public DbContextHealthCheck(TContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("database reachable")
                : HealthCheckResult.Unhealthy("database not reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("database check failed", ex);
        }
    }
}

/// <summary>
/// One health-endpoint contract for every GDB service, on top of the framework health-check
/// subsystem instead of hand-rolled controllers returning constants (audit Finding #15).
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><description><c>/live</c> — liveness: the process is up and serving HTTP. Runs no checks.</description></item>
///   <item><description><c>/ready</c> — readiness: every check tagged <see cref="ReadyTag"/> (the database probe) must pass,
///   so an orchestrator stops routing traffic to an instance whose dependencies are down.</description></item>
/// </list>
/// Both are anonymous by design (probes carry no token) and return a small JSON report.
/// </remarks>
public static class GdbHealthExtensions
{
    public const string ReadyTag = "ready";

    public static IHealthChecksBuilder AddGdbHealthChecks(this IServiceCollection services) => services.AddHealthChecks();

    /// <summary>Adds a readiness-gating database connectivity probe for <typeparamref name="TContext"/>.</summary>
    public static IHealthChecksBuilder AddDatabase<TContext>(this IHealthChecksBuilder builder, string name = "database") where TContext : DbContext
        => builder.AddCheck<DbContextHealthCheck<TContext>>(name, failureStatus: HealthStatus.Unhealthy, tags: new[] { ReadyTag });

    public static WebApplication MapGdbHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/live", new HealthCheckOptions
        {
            Predicate = _ => false, // liveness answers as soon as the pipeline does; no dependency checks
            ResponseWriter = WriteReportAsync
        }).AllowAnonymous();

        app.MapHealthChecks("/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = WriteReportAsync
        }).AllowAnonymous();

        return app;
    }

    private static Task WriteReportAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            duration_ms = (long)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString().ToLowerInvariant(),
                description = e.Value.Description,
                duration_ms = (long)e.Value.Duration.TotalMilliseconds
            })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}

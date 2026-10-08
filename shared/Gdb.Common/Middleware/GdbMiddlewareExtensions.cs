using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Gdb.Common.Middleware;

/// <summary>
/// Registers and wires the cross-cutting middleware every GDB service runs: correlation/trace-id
/// propagation (+ log scope), security response headers, the single error boundary and the
/// Prometheus request metrics behind <c>/metrics</c> (see <see cref="MetricsEndpointExtensions.MapGdbMetrics"/>).
/// </summary>
public static class GdbMiddlewareExtensions
{
    /// <summary>DI registration for the factory-activated (<c>IMiddleware</c>) cross-cutting middleware.</summary>
    public static IServiceCollection AddGdbCrossCuttingMiddleware(this IServiceCollection services)
    {
        services.AddTransient<CorrelationIdMiddleware>();
        services.AddTransient<SecurityHeadersMiddleware>();
        services.AddTransient<ExceptionHandlingMiddleware>();
        services.AddSingleton<MetricsRegistry>();
        services.AddTransient<MetricsMiddleware>();
        return services;
    }

    /// <summary>
    /// Pipeline wiring — outermost, so the correlation id exists first, security headers reach
    /// every response, the error boundary can stamp the correlation id on error bodies, and the
    /// metrics observe the final status code of every request (errors included).
    /// </summary>
    public static IApplicationBuilder UseGdbCrossCuttingMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<MetricsMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        return app;
    }
}

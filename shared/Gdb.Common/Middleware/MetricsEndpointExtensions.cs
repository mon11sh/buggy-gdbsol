using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Gdb.Common.Middleware;

public static class MetricsEndpointExtensions
{
    public static IEndpointRouteBuilder MapGdbMetrics(this IEndpointRouteBuilder builder, string pattern = "/metrics")
    {
        builder.MapGet(pattern, async context =>
        {
            var registry = context.RequestServices.GetRequiredService<MetricsRegistry>();
            var metricsText = registry.Render();
            context.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
            await context.Response.WriteAsync(metricsText);
        }).ExcludeFromDescription(); // Equivalent to include_in_schema=False in FastAPI

        return builder;
    }
}

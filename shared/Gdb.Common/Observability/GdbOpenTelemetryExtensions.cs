using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Gdb.Common.Observability;

/// <summary>
/// Distributed tracing + metrics export over OTLP, switched on by the standard
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> environment variable (e.g. an OpenTelemetry Collector,
/// Jaeger, Tempo, Grafana Cloud). Without it nothing is registered: no collector, no overhead.
/// The in-process Prometheus <c>/metrics</c> endpoint keeps working either way.
/// </summary>
public static class GdbOpenTelemetryExtensions
{
    public const string EndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public static IServiceCollection AddGdbOpenTelemetry(this IServiceCollection services, string serviceName)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EndpointVariable)))
            return services;

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t => t
                .AddSource("Gdb.Common.Observability")
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/metrics"))
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter());

        return services;
    }
}

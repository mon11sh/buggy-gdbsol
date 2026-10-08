using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Gdb.Common.Middleware;

/// <summary>
/// Establishes the request's correlation id and W3C trace context, echoes them on the response,
/// and opens an <see cref="ILogger"/> scope so EVERY log line written while handling the request
/// carries <c>CorrelationId</c>/<c>TraceId</c> (the NLog JSON layout emits them as fields).
/// </summary>
public class CorrelationIdMiddleware : IMiddleware
{
    private static readonly ActivitySource ActivitySource = new("Gdb.Common.Observability");
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(ILogger<CorrelationIdMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].ToString();
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Items["CorrelationId"] = correlationId;

        // Trace Context (traceparent)
        var traceparent = context.Request.Headers["traceparent"].ToString();
        var traceId = "";
        var spanId = Guid.NewGuid().ToString("N").Substring(0, 16);

        if (!string.IsNullOrEmpty(traceparent) && traceparent.StartsWith("00-"))
        {
            var parts = traceparent.Split('-');
            if (parts.Length == 4 && parts[1].Length == 32 && parts[2].Length == 16)
            {
                traceId = parts[1];
            }
        }

        if (string.IsNullOrEmpty(traceId))
        {
            traceId = Guid.NewGuid().ToString("N");
        }

        context.Items["TraceId"] = traceId;
        context.Items["SpanId"] = spanId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-ID"] = correlationId;
            context.Response.Headers["traceparent"] = $"00-{traceId}-{spanId}-01";
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = traceId,
        }))
        {
            await next(context);
        }
    }
}

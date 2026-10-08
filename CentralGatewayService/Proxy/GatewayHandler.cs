using CentralGatewayService.Resolvers;

namespace CentralGatewayService.Proxy;

public static class GatewayHandler
{
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "connection", "keep-alive", "proxy-authenticate", "proxy-authorization",
        "te", "trailers", "transfer-encoding", "upgrade", "host", "content-length"
    };

    // Internal service-to-service trust headers must NEVER be accepted from an external client
    // at the public edge — they are injected only by internal callers behind the gateway.
    // Stripping them here prevents a client from forging internal access via the proxy.
    // (Authorization is intentionally NOT stripped: user JWTs must flow through to backends.)
    private static readonly HashSet<string> ClientStrippedRequestHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "x-internal-api-key",
    };

    public static async Task HandleProxyRequest(HttpContext context, IServiceResolver resolver, IHttpClientFactory httpClientFactory)
    {
        string service;
        string path = string.Empty;

        var routeValues = context.Request.RouteValues;
        if (routeValues.TryGetValue("service", out var serviceObj) && serviceObj is string srv)
        {
            service = srv;
            routeValues.TryGetValue("path", out var pathObj);
            path = pathObj as string ?? string.Empty;
        }
        else
        {
            var pathValue = context.Request.Path.Value?.TrimStart('/');
            if (string.IsNullOrEmpty(pathValue))
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsJsonAsync(new { detail = "unknown or unavailable service ''" });
                return;
            }
            var parts = pathValue.Split('/', 2);
            service = parts[0];
            if (parts.Length > 1) path = parts[1];
        }

        var baseUrl = await resolver.ResolveAsync(service);
        if (string.IsNullOrEmpty(baseUrl))
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { detail = $"unknown or unavailable service '{service}'" });
            return;
        }

        var targetUrl = $"{baseUrl.TrimEnd('/')}/{path}{context.Request.QueryString}";
        var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);

        // Copy Headers
        var incomingHost = context.Request.Headers.Host.ToString();
        foreach (var header in context.Request.Headers)
        {
            if (!HopByHopHeaders.Contains(header.Key) && !ClientStrippedRequestHeaders.Contains(header.Key))
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }
        
        // Preserve client Host
        if (!string.IsNullOrEmpty(incomingHost))
        {
            requestMessage.Headers.Host = incomingHost;
        }

        // Standard forwarding chain so backends can log/limit on the real client, not the gateway.
        var clientIp = context.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(clientIp))
            requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-For", clientIp);
        requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-Proto", context.Request.Scheme);
        if (!string.IsNullOrEmpty(incomingHost))
            requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-Host", incomingHost);

        // Propagate Correlation ID and Trace Context
        if (context.Items.TryGetValue("CorrelationId", out var correlationId))
        {
            requestMessage.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId?.ToString());
        }
        if (context.Items.TryGetValue("TraceId", out var traceId) && context.Items.TryGetValue("SpanId", out var spanId))
        {
            requestMessage.Headers.TryAddWithoutValidation("traceparent", $"00-{traceId}-{spanId}-01");
        }

        // Copy Body
        if (context.Request.ContentLength > 0 || context.Request.Headers.TransferEncoding.Count > 0)
        {
            requestMessage.Content = new StreamContent(context.Request.Body);
            if (context.Request.ContentType != null)
            {
                requestMessage.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
            }
        }

        var client = httpClientFactory.CreateClient("GatewayClient");
        
        try
        {
            using var responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            context.Response.StatusCode = (int)responseMessage.StatusCode;

            foreach (var header in responseMessage.Headers)
            {
                if (!HopByHopHeaders.Contains(header.Key))
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }
            }
            foreach (var header in responseMessage.Content.Headers)
            {
                if (!HopByHopHeaders.Contains(header.Key))
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }
            }

            context.Response.Headers.Remove("transfer-encoding");
            
            await responseMessage.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (HttpRequestException ex)
        {
            context.Response.StatusCode = 502;
            await context.Response.WriteAsJsonAsync(new { detail = $"backend '{service}' unreachable: {ex.GetType().Name}" });
        }
        catch (TaskCanceledException ex) when (!context.RequestAborted.IsCancellationRequested)
        {
             context.Response.StatusCode = 502;
             await context.Response.WriteAsJsonAsync(new { detail = $"backend '{service}' unreachable: {ex.GetType().Name}" });
        }
    }
}

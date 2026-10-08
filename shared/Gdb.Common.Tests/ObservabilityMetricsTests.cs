using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Moq;
using Gdb.Common.Middleware;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gdb.Common.Tests;

[TestClass]
public class ObservabilityMetricsTests
{
    [TestMethod]
    public async Task Metrics_Endpoint_Prometheus_Format()
    {
        var registry = new MetricsRegistry();
        registry.SetService("obs-test");

        var middleware = new MetricsMiddleware(registry);

        var context1 = new DefaultHttpContext();
        context1.Request.Method = "GET";
        context1.Request.Path = "/api/v1/thing/1";
        context1.Response.StatusCode = 200;
        context1.SetEndpoint(new RouteEndpoint(c => Task.CompletedTask, RoutePatternFactory.Parse("/api/v1/thing/{thing_id}"), 0, null, null));

        await middleware.InvokeAsync(context1, c => Task.CompletedTask);

        var context2 = new DefaultHttpContext();
        context2.Request.Method = "GET";
        context2.Request.Path = "/api/v1/thing/2";
        context2.Response.StatusCode = 200;
        context2.SetEndpoint(new RouteEndpoint(c => Task.CompletedTask, RoutePatternFactory.Parse("/api/v1/thing/{thing_id}"), 0, null, null));

        await middleware.InvokeAsync(context2, c => Task.CompletedTask);

        var body = registry.Render();

        Assert.Contains("http_requests_total", body);
        Assert.Contains("http_request_duration_seconds_bucket", body);
        Assert.Contains("http_request_duration_seconds_count", body);
        Assert.Contains("http_requests_in_progress", body);
        
        // Ensure path uses route template, not raw path
        Assert.Contains("path=\"/api/v1/thing/{thing_id}\"", body);
        Assert.DoesNotContain("path=\"/api/v1/thing/1\"", body);
    }
}

using System.Text.Json;
using Gdb.Common.Exceptions;
using Gdb.Common.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gdb.Common.Tests;

[TestClass]
public class ExceptionHandlingMiddlewareTests
{
    private sealed class LockedException : GdbException
    {
        public LockedException() : base("Too many attempts", "ACCOUNT_LOCKED", 423) { RetryAfterSeconds = 60; }
    }

    private static async Task<(DefaultHttpContext ctx, JsonElement body)> RunAsync(Exception toThrow)
    {
        var ctx = new DefaultHttpContext();
        ctx.Items["CorrelationId"] = "corr-1";
        ctx.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(NullLogger<ExceptionHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(ctx, _ => throw toThrow);

        ctx.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(ctx.Response.Body);
        return (ctx, body.RootElement);
    }

    [TestMethod]
    public async Task GdbException_MapsDeclaredStatusCodeAndRetryAfter()
    {
        var (ctx, body) = await RunAsync(new LockedException());

        Assert.AreEqual(423, ctx.Response.StatusCode);
        Assert.AreEqual("60", ctx.Response.Headers.RetryAfter.ToString());
        Assert.AreEqual("ACCOUNT_LOCKED", body.GetProperty("error_code").GetString());
        Assert.AreEqual("Too many attempts", body.GetProperty("message").GetString());
        Assert.AreEqual("Too many attempts", body.GetProperty("detail").GetString());
        Assert.AreEqual("corr-1", body.GetProperty("correlation_id").GetString());
    }

    [TestMethod]
    public async Task UnexpectedException_Is500_AndNeverLeaksTheMessage()
    {
        var (ctx, body) = await RunAsync(new InvalidOperationException("connection string: Password=secret"));

        Assert.AreEqual(500, ctx.Response.StatusCode);
        Assert.AreEqual("INTERNAL_ERROR", body.GetProperty("error_code").GetString());
        Assert.DoesNotContain("secret", body.GetProperty("message").GetString()!);
    }
}

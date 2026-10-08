using Gdb.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Gdb.Common.Middleware;

/// <summary>
/// The single error boundary for every GDB service. Expected failures (<see cref="GdbException"/>)
/// become their declared status + code; anything else is logged with the stack trace and answered
/// as an opaque 500 so internals never leak. Wire shape is the one the frontend already parses:
/// <c>{ error_code, message, detail, status, correlation_id, request_id }</c>.
/// </summary>
public class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (GdbException ex) when (!context.Response.HasStarted)
        {
            // Expected outcome: a warning with context, never a stack trace.
            _logger.LogWarning("{ErrorCode} ({StatusCode}) on {Method} {Path}: {Message}",
                ex.ErrorCode, ex.StatusCode, context.Request.Method, context.Request.Path, ex.Message);

            if (ex.RetryAfterSeconds is int seconds)
                context.Response.Headers.RetryAfter = seconds.ToString();

            await WriteAsync(context, ex.StatusCode, ex.ErrorCode, ex.Message);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            _logger.LogError(ex, "Unhandled {ExceptionType} on {Method} {Path}",
                ex.GetType().Name, context.Request.Method, context.Request.Path);

            await WriteAsync(context, StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred");
        }
    }

    private static Task WriteAsync(HttpContext context, int statusCode, string errorCode, string message)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? "-";
        var requestId = context.Request.Headers["X-Request-ID"].ToString();
        if (string.IsNullOrEmpty(requestId)) requestId = correlationId;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            error_code = errorCode,
            message,
            detail = message,           // some UI pages read "detail", others "message"
            status = "error",
            correlation_id = correlationId,
            request_id = requestId
        }));
    }
}

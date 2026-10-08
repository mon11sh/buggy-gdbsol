using Microsoft.AspNetCore.Http;

namespace Gdb.Common.Middleware;

public class SecurityHeadersMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("X-Content-Type-Options"))
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            
            if (!context.Response.Headers.ContainsKey("X-Frame-Options"))
                context.Response.Headers["X-Frame-Options"] = "DENY";
            
            if (!context.Response.Headers.ContainsKey("Referrer-Policy"))
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
            
            if (!context.Response.Headers.ContainsKey("Strict-Transport-Security"))
                context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            
            if (!context.Response.Headers.ContainsKey("Permissions-Policy"))
                context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

            var path = context.Request.Path.Value ?? "";
            if (!path.Contains("/docs") && !path.Contains("/swagger") && !path.Contains("/redoc") && !path.Contains("/openapi"))
            {
                if (!context.Response.Headers.ContainsKey("Content-Security-Policy"))
                    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}

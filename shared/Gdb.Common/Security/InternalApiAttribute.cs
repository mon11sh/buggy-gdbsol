using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Gdb.Common.Security;

/// <summary>
/// Secures endpoints intended only for service-to-service communication within the internal network.
/// </summary>
/// <remarks>
/// Architectural Intent: This prevents external clients from invoking privileged internal APIs
/// (like resolving full customer details) through the gateway.
/// Security Intent: Validation uses a constant-time comparison to prevent timing attacks.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class InternalApiAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var apiKeyProvider = context.HttpContext.RequestServices.GetService<Func<string>>();
        
        var expectedKey = apiKeyProvider?.Invoke();
        if (string.IsNullOrEmpty(expectedKey))
        {
            // If the application doesn't provide a key, we reject by default to be safe.
            context.Result = new UnauthorizedObjectResult(new
            {
                error_code = "UNAUTHORIZED",
                message = "Internal API key is not configured"
            });
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue("X-Internal-API-Key", out var providedKeyHeader))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                error_code = "UNAUTHORIZED",
                message = "Missing or invalid internal API key"
            });
            return;
        }

        var providedKey = providedKeyHeader.ToString();

        // Constant time comparison
        // Security Intent: Prevents timing attacks where an attacker could theoretically 
        // guess the API key character-by-character based on response latency.
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(providedKey),
                System.Text.Encoding.UTF8.GetBytes(expectedKey)))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                error_code = "UNAUTHORIZED",
                message = "Missing or invalid internal API key"
            });
        }
    }
}

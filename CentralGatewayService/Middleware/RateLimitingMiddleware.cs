using CentralGatewayService.Config;
using System.Collections.Concurrent;

namespace CentralGatewayService.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly int _capacity;
    private readonly double _refillPerSec;
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();

    public RateLimitingMiddleware(RequestDelegate next, Settings settings)
    {
        _next = next;
        
        var perMin = settings.RateLimitPerMin;
        if (perMin > 0)
        {
            var burst = settings.RateBurst > 0 ? settings.RateBurst : perMin;
            _capacity = Math.Max(1, burst);
            _refillPerSec = perMin / 60.0;
        }
        else
        {
            _capacity = 0; // Disabled
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Exempt health checks and OPTIONS
        if (_capacity <= 0 || context.Request.Method == "OPTIONS" || context.Request.Path == "/health")
        {
            await _next(context);
            return;
        }

        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var bucket = _buckets.GetOrAdd(clientIp, _ => new TokenBucket(_capacity, _refillPerSec));

        if (!bucket.TryConsume(1, out var retryAfter))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter)).ToString();
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { detail = "rate limit exceeded" });
            return;
        }

        await _next(context);
    }

    private class TokenBucket
    {
        private readonly int _capacity;
        private readonly double _refillPerSec;
        private double _tokens;
        private DateTime _lastRefill;
        private readonly object _lock = new();

        public TokenBucket(int capacity, double refillPerSec)
        {
            _capacity = capacity;
            _refillPerSec = refillPerSec;
            _tokens = capacity;
            _lastRefill = DateTime.UtcNow;
        }

        public bool TryConsume(int amount, out double retryAfterSeconds)
        {
            lock (_lock)
            {
                Refill();
                if (_tokens >= amount)
                {
                    _tokens -= amount;
                    retryAfterSeconds = 0;
                    return true;
                }
                
                var deficit = amount - _tokens;
                retryAfterSeconds = deficit / _refillPerSec;
                return false;
            }
        }

        private void Refill()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastRefill).TotalSeconds;
            _tokens = Math.Min(_capacity, _tokens + elapsed * _refillPerSec);
            _lastRefill = now;
        }
    }
}

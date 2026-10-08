using System.Collections.Concurrent;

namespace Gdb.Common.Middleware;

public class RateLimiter
{
    public int Capacity { get; }
    public double RefillPerSec { get; }

    private readonly ConcurrentDictionary<string, (double Tokens, long LastTs)> _buckets = new();
    private readonly object _lock = new();

    protected virtual long GetNow() => Environment.TickCount64;

    public RateLimiter(int capacity, double refillPerSec)
    {
        if (capacity < 1) throw new ArgumentException("capacity must be >= 1");
        if (refillPerSec <= 0) throw new ArgumentException("refillPerSec must be > 0");

        Capacity = capacity;
        RefillPerSec = refillPerSec;
    }

    public (bool Allowed, double RetryAfter) Check(string key)
    {
        var now = GetNow();
        lock (_lock)
        {
            var (tokens, lastTs) = _buckets.GetOrAdd(key, _ => (Capacity, now));

            var elapsedSeconds = (now - lastTs) / 1000.0;
            tokens = Math.Min(Capacity, tokens + elapsedSeconds * RefillPerSec);

            if (tokens >= 1.0)
            {
                _buckets[key] = (tokens - 1.0, now);
                return (true, 0.0);
            }

            var retryAfter = (1.0 - tokens) / RefillPerSec;
            _buckets[key] = (tokens, now);
            return (false, retryAfter);
        }
    }

    public void Reset(string? key = null)
    {
        if (key == null)
        {
            _buckets.Clear();
        }
        else
        {
            _buckets.TryRemove(key, out _);
        }
    }
}

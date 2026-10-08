using System.Buffers.Binary;
using Microsoft.Extensions.Caching.Distributed;

namespace Gdb.Common.Caching;

/// <summary>
/// "N failures within a window locks the subject for the window" — the one policy behind PIN
/// lockouts and login throttling, kept in the shared <see cref="IDistributedCache"/> so every
/// replica sees the same counters. Subjects are opaque strings (account number, login id).
/// </summary>
/// <remarks>
/// ponytail: increment is read-modify-write, not atomic. Two replicas racing on the SAME subject
/// in the same instant can lose one increment, i.e. grant one extra attempt — acceptable for a
/// brute-force guard. Switch to a Lua/INCR script on the Redis connection if that ever matters.
/// </remarks>
public sealed class FailureLockout
{
    private readonly IDistributedCache _cache;
    private readonly string _prefix;
    private readonly int _maxFailures;
    private readonly TimeSpan _window;

    public FailureLockout(IDistributedCache cache, string prefix, int maxFailures, TimeSpan window)
    {
        _cache = cache;
        _prefix = prefix;
        _maxFailures = maxFailures;
        _window = window;
    }

    /// <summary>Remaining lock time, or null when the subject is not locked.</summary>
    public TimeSpan? LockedFor(string subject)
    {
        var bytes = _cache.Get(LockKey(subject));
        if (bytes is null || bytes.Length != 8) return null;

        var until = new DateTime(BinaryPrimitives.ReadInt64LittleEndian(bytes), DateTimeKind.Utc);
        var remaining = until - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    /// <summary>Records a failure; returns true when this failure locked the subject.</summary>
    public bool RecordFailure(string subject)
    {
        var key = CountKey(subject);
        var bytes = _cache.Get(key);
        var count = (bytes is { Length: 4 } ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : 0) + 1;

        if (count < _maxFailures)
        {
            var buffer = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, count);
            _cache.Set(key, buffer, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _window });
            return false;
        }

        var untilBuffer = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(untilBuffer, (DateTime.UtcNow + _window).Ticks);
        _cache.Set(LockKey(subject), untilBuffer, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _window });
        _cache.Remove(key);
        return true;
    }

    public void Reset(string subject)
    {
        _cache.Remove(CountKey(subject));
        _cache.Remove(LockKey(subject));
    }

    private string CountKey(string subject) => $"{_prefix}:fail:{subject}";
    private string LockKey(string subject) => $"{_prefix}:lock:{subject}";
}

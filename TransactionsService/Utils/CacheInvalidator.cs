using System.Buffers.Binary;
using Microsoft.Extensions.Caching.Distributed;

namespace TransactionsService.Utils;

/// <summary>
/// Cross-instance invalidation for the per-account transaction-history cache. Each account has a
/// version number in the shared distributed cache; cached pages embed the version in their key, so
/// bumping it on any replica makes every replica's cached pages for that account unreachable
/// (they then age out by TTL). No change tokens, no fan-out.
/// </summary>
public class CacheInvalidator
{
    private static readonly TimeSpan VersionTtl = TimeSpan.FromDays(1);
    private readonly IDistributedCache _cache;

    public CacheInvalidator(IDistributedCache cache)
    {
        _cache = cache;
    }

    public long VersionOf(int accountId)
    {
        var bytes = _cache.Get(Key(accountId));
        return bytes is { Length: 8 } ? BinaryPrimitives.ReadInt64LittleEndian(bytes) : 0;
    }

    public void EvictAccount(int accountId)
    {
        // ponytail: read-modify-write; a lost race just re-bumps to the same number, which is harmless.
        var buffer = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, VersionOf(accountId) + 1);
        _cache.Set(Key(accountId), buffer, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = VersionTtl });
    }

    private static string Key(int accountId) => $"txhist:ver:{accountId}";
}

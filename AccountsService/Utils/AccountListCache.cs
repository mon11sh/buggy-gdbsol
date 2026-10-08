using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using AccountsService.Domain.Models;

namespace AccountsService.Utils;

/// <summary>
/// Process-wide cache for the account LIST read path, keyed by the account-type filter.
/// </summary>
/// <remarks>
/// It owns a <see cref="Timer"/> — a disposable resource that also roots this object on the GC
/// finalization queue — so it implements the full IDisposable + finalizer pattern that .NET uses
/// to manage non-memory resources alongside the garbage collector:
/// <list type="bullet">
///   <item><description><c>Dispose()</c> — deterministic cleanup that callers invoke.</description></item>
///   <item><description><c>Dispose(bool)</c> — the shared core; releases the timer on an explicit dispose.</description></item>
///   <item><description><c>~AccountListCache()</c> — a finalizer safety-net, run by the GC if Dispose is never called.</description></item>
///   <item><description><c>GC.SuppressFinalize(this)</c> — tells the GC to skip finalization once we cleaned up ourselves.</description></item>
/// </list>
/// Registered as a <b>SINGLETON</b>: it must live for the whole application, not per request.
/// Disposing this shared instance (or registering it scoped/transient so the DI container disposes
/// it at the end of a request) makes every later read throw <see cref="ObjectDisposedException"/>.
/// </remarks>
public sealed class AccountListCache : IDisposable
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(15);

    private readonly ConcurrentDictionary<string, (List<Account> Accounts, DateTime Expiry)> _entries = new();
    private readonly Timer _sweeper;
    private bool _disposed;

    public AccountListCache()
    {
        // Evict expired snapshots every 15s so a rarely-used filter can't pin memory forever.
        _sweeper = new Timer(_ => Sweep(), null, Ttl, Ttl);
    }

    private static string KeyFor(string? accountType) => accountType ?? "__ALL__";

    public bool TryGet(string? accountType, out List<Account> accounts)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_entries.TryGetValue(KeyFor(accountType), out var entry) && entry.Expiry > DateTime.UtcNow)
        {
            accounts = entry.Accounts;
            return true;
        }
        accounts = new List<Account>();
        return false;
    }

    public void Set(string? accountType, List<Account> accounts)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _entries[KeyFor(accountType)] = (accounts, DateTime.UtcNow.Add(Ttl));
    }

    /// <summary>Drops every cached snapshot — call after any write that changes the account list.</summary>
    public void Invalidate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _entries.Clear();
    }

    private void Sweep()
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _entries)
        {
            if (kv.Value.Expiry <= now)
                _entries.TryRemove(kv.Key, out _);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _sweeper.Dispose(); // release the timer (a disposable resource)
            _entries.Clear();
        }

        _disposed = true;
    }

    // Finalizer: safety net so the timer is still released if Dispose was never called.
    ~AccountListCache() => Dispose(false);
}

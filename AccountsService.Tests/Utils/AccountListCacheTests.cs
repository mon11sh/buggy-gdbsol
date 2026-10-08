using System;
using System.Collections.Generic;
using AccountsService.Domain.Enums;
using AccountsService.Domain.Models;
using AccountsService.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountsService.Tests.Utils;

/// <summary>
/// Behaviour of the disposable AccountListCache, including the IDisposable/GC contract:
/// once disposed (which releases its Timer), every operation must throw ObjectDisposedException.
/// </summary>
[TestClass]
public class AccountListCacheTests
{
    private static List<Account> OneAccount() => new()
    {
        SavingsAccount.RestoreSavings(new AccountId(1001), "SAVINGS", "Test", "SILVER", "hash",
            new Money(5000m), new Bank("GDB", "Main", "GDB0000001"), AccountStatus.ACTIVE, DateTime.UtcNow, null,
            new SavingsDetails("1990-01-01", "M", "9999999999", "enc", "hash"))
    };

    [TestMethod]
    public void Set_ThenTryGet_ReturnsCachedList()
    {
        using var cache = new AccountListCache();
        cache.Set("SAVINGS", OneAccount());

        Assert.IsTrue(cache.TryGet("SAVINGS", out var list));
        Assert.HasCount(1, list);
    }

    [TestMethod]
    public void TryGet_Miss_ReturnsFalse()
    {
        using var cache = new AccountListCache();
        Assert.IsFalse(cache.TryGet("CURRENT", out var list));
        Assert.IsEmpty(list);
    }

    [TestMethod]
    public void Invalidate_ClearsCachedEntries()
    {
        using var cache = new AccountListCache();
        cache.Set(null, OneAccount());
        cache.Invalidate();
        Assert.IsFalse(cache.TryGet(null, out _));
    }

    [TestMethod]
    public void AfterDispose_Operations_ThrowObjectDisposed()
    {
        var cache = new AccountListCache();
        cache.Set("SAVINGS", OneAccount());
        cache.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => cache.TryGet("SAVINGS", out _));
        Assert.ThrowsExactly<ObjectDisposedException>(() => cache.Set("SAVINGS", OneAccount()));
        Assert.ThrowsExactly<ObjectDisposedException>(() => cache.Invalidate());
    }

    [TestMethod]
    public void Dispose_IsIdempotent()
    {
        var cache = new AccountListCache();
        cache.Dispose();
        cache.Dispose(); // second dispose must not throw
    }
}

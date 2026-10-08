using AccountsService.Domain.Exceptions;
using AccountsService.Utils;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountsService.Tests.Utils;

/// <summary>Brute-force lockout contract shared by the public and internal PIN paths.</summary>
[TestClass]
public class PinLockoutServiceTests
{
    private static PinLockoutService New() => new(new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions())));

    [TestMethod]
    public void FiveConsecutiveFailures_LockTheAccount()
    {
        var lockout = New();
        for (var i = 0; i < 5; i++) lockout.RecordFailure(1001);

        Assert.ThrowsExactly<AccountLockedError>(() => lockout.CheckLocked(1001));
    }

    [TestMethod]
    public void FourFailures_DoNotLock()
    {
        var lockout = New();
        for (var i = 0; i < 4; i++) lockout.RecordFailure(1001);

        lockout.CheckLocked(1001); // must not throw
    }

    [TestMethod]
    public void Success_ResetsTheCounter()
    {
        var lockout = New();
        for (var i = 0; i < 4; i++) lockout.RecordFailure(1001);
        lockout.RecordSuccess(1001);
        for (var i = 0; i < 4; i++) lockout.RecordFailure(1001);

        lockout.CheckLocked(1001); // 4 + reset + 4 never reaches the threshold
    }

    [TestMethod]
    public void Lockout_IsScopedToTheAccount()
    {
        var lockout = New();
        for (var i = 0; i < 5; i++) lockout.RecordFailure(1001);

        lockout.CheckLocked(1002); // a different account is unaffected
    }
}

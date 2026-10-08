using Gdb.Common.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Gdb.Common.Tests;

[TestClass]
public class FailureLockoutTests
{
    private static FailureLockout NewLockout(int maxFailures = 3) =>
        new(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), "t", maxFailures, TimeSpan.FromMinutes(15));

    [TestMethod]
    public void Locks_on_the_nth_failure_and_reports_remaining_time()
    {
        var lockout = NewLockout(maxFailures: 3);

        Assert.IsFalse(lockout.RecordFailure("acct-1"));
        Assert.IsFalse(lockout.RecordFailure("acct-1"));
        Assert.IsNull(lockout.LockedFor("acct-1"), "two failures are still allowed");

        Assert.IsTrue(lockout.RecordFailure("acct-1"), "third failure locks");
        var remaining = lockout.LockedFor("acct-1");
        Assert.IsNotNull(remaining);
        Assert.IsTrue(remaining.Value > TimeSpan.FromMinutes(14) && remaining.Value <= TimeSpan.FromMinutes(15));
    }

    [TestMethod]
    public void Subjects_are_independent_and_reset_clears_everything()
    {
        var lockout = NewLockout(maxFailures: 2);

        lockout.RecordFailure("a");
        lockout.RecordFailure("a");
        Assert.IsNotNull(lockout.LockedFor("a"));
        Assert.IsNull(lockout.LockedFor("b"));

        lockout.Reset("a");
        Assert.IsNull(lockout.LockedFor("a"));
        Assert.IsFalse(lockout.RecordFailure("a"), "counter restarted from zero");
    }
}

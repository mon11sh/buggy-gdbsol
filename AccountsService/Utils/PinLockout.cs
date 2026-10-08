using AccountsService.Domain.Exceptions;
using Gdb.Common.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace AccountsService.Utils;

public interface IPinLockoutService
{
    void CheckLocked(int accountNumber);
    void RecordSuccess(int accountNumber);
    void RecordFailure(int accountNumber);
}

/// <summary>
/// Brute-force guard shared by the public and internal PIN paths: 5 failures within 15 minutes lock
/// the account for 15 minutes. State lives in the shared distributed cache (Redis when configured),
/// so the counter is the same on every replica.
/// </summary>
public class PinLockoutService : IPinLockoutService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);

    private readonly FailureLockout _lockout;

    public PinLockoutService(IDistributedCache cache)
    {
        _lockout = new FailureLockout(cache, "pin", MaxAttempts, LockoutWindow);
    }

    public void CheckLocked(int accountNumber)
    {
        if (_lockout.LockedFor(accountNumber.ToString()) is TimeSpan remaining)
        {
            throw new AccountLockedError(
                $"Account locked due to too many failed PIN attempts. Try again in {Math.Ceiling(remaining.TotalMinutes)} minutes.",
                (int)Math.Ceiling(remaining.TotalSeconds));
        }
    }

    public void RecordSuccess(int accountNumber) => _lockout.Reset(accountNumber.ToString());

    public void RecordFailure(int accountNumber) => _lockout.RecordFailure(accountNumber.ToString());
}

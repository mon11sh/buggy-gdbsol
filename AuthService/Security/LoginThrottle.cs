using Gdb.Common.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace AuthService.Security;

/// <summary>
/// Login brute-force guard: 5 failed logins within 15 minutes lock the login id for 15 minutes.
/// State lives in the shared distributed cache (Redis when configured), so an attacker cannot get a
/// fresh budget from every replica.
/// </summary>
public class LoginThrottle
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);

    private readonly FailureLockout _lockout;
    private readonly ILogger<LoginThrottle> _logger;

    public LoginThrottle(IDistributedCache cache, ILogger<LoginThrottle> logger)
    {
        _lockout = new FailureLockout(cache, "login", MaxFailures, LockoutWindow);
        _logger = logger;
    }

    public bool IsLocked(string loginId) => _lockout.LockedFor(loginId) is not null;

    public void RecordFailure(string loginId)
    {
        if (_lockout.RecordFailure(loginId))
            _logger.LogWarning("Login locked out for '{LoginId}' after {FailCount} failures for {Window}", loginId, MaxFailures, LockoutWindow);
    }

    public void RecordSuccess(string loginId) => _lockout.Reset(loginId);
}

using System;
using System.Collections.Generic;

namespace AccountsService.Domain.Models;

/// <summary>
/// Privilege-based monthly interest strategy.
/// </summary>
/// <remarks>
/// Uses a dictionary of <see cref="Func{T, TResult}"/> delegates (privilege → rate function)
/// instead of a scattered switch. Each delegate takes the current balance and returns the
/// monthly interest for that privilege tier, so adding a tier is a one-line data change.
/// </remarks>
public static class InterestPolicy
{
    // Annual rates, applied per-month (rate / 12).
    public static readonly IReadOnlyDictionary<string, Func<decimal, decimal>> MonthlyInterest =
        new Dictionary<string, Func<decimal, decimal>>(StringComparer.OrdinalIgnoreCase)
        {
            ["SILVER"] = balance => balance * 0.03m / 12m,
            ["GOLD"] = balance => balance * 0.04m / 12m,
            ["PREMIUM"] = balance => balance * 0.05m / 12m,
        };

    /// <summary>Fallback used when a privilege has no configured rate function.</summary>
    public static readonly Func<decimal, decimal> NoInterest = _ => 0m;

    /// <summary>Resolves the interest delegate for a privilege, falling back to <see cref="NoInterest"/>.</summary>
    public static Func<decimal, decimal> ResolveFor(string privilege)
        => MonthlyInterest.TryGetValue(privilege ?? string.Empty, out var fn) ? fn : NoInterest;
}

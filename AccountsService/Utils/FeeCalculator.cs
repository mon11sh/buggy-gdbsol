namespace AccountsService.Utils;

/// <summary>
/// Small numeric helpers for account fee/balance arithmetic.
/// </summary>
public static class FeeCalculator
{
    /// <summary>
    /// Deducts <paramref name="amount"/> from the caller's running balance in place.
    /// Uses a <c>ref</c> parameter so the caller's own variable is mutated rather than a copy —
    /// the value is read, modified, and written back through the same reference.
    /// </summary>
    public static void Deduct(ref decimal balance, decimal amount)
    {
        balance -= amount;
    }
}

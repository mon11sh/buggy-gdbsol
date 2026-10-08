using System;

namespace AccountsService.Domain.Models;

/// <summary>
/// Monetary value object. Enforces the invariant that an amount is never negative and carries
/// its currency (default INR) instead of leaving currency as a magic string at the edges.
/// </summary>
public record Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; }

    public Money(decimal amount, string currency = "INR")
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Money amount cannot be negative.");
        Amount = amount;
        Currency = currency;
    }

    public Money Add(Money other) => new(Amount + other.Amount, Currency);

    // Method overloading: the same operation expressed for a raw decimal amount as well as a
    // Money value, so callers with a plain amount don't have to wrap it first. Both overloads
    // keep this instance's currency.
    public Money Add(decimal amount) => Add(new Money(amount, Currency));

    public Money Subtract(decimal amount) => Subtract(new Money(amount, Currency));

    public Money Subtract(Money other)
    {
        // The domain (Account.Debit) checks sufficient funds before subtracting; this guard
        // enforces the non-negative invariant defensively so a bad call can't mint negative money.
        if (other.Amount > Amount)
            throw new InvalidOperationException("Resulting money cannot be negative.");
        return new Money(Amount - other.Amount, Currency);
    }
}

using TransactionsService.Domain.Exceptions;

namespace TransactionsService.Domain.Models;

public record Money
{
    public decimal Amount { get; }

    public Money(decimal amount)
    {
        if (amount < 0)
            throw new InvalidAmountException("Money amount cannot be negative.");
        Amount = Math.Round(amount, 2);
    }

    public Money Add(Money other)
    {
        return new Money(Amount + other.Amount);
    }

    public Money Subtract(Money other)
    {
        if (Amount < other.Amount)
            throw new InvalidAmountException("Cannot subtract to a negative amount.");
        return new Money(Amount - other.Amount);
    }
}

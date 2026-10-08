using System;
using TransactionsService.Domain.Exceptions;

namespace TransactionsService.Domain.Models;

public class TransferLimit
{
    public string Privilege { get; }
    public Money DailyLimit { get; }
    public Money PerTransactionLimit { get; }

    public TransferLimit(string privilege, Money dailyLimit, Money perTransactionLimit)
    {
        if (string.IsNullOrWhiteSpace(privilege))
            throw new ArgumentException("Privilege cannot be empty.");
            
        Privilege = privilege;
        DailyLimit = dailyLimit;
        PerTransactionLimit = perTransactionLimit;
    }

    public void CheckLimits(Money amount, Money dailyUsed, int dailyCount)
    {
        var dailyRemaining = DailyLimit.Amount - dailyUsed.Amount;
        if (amount.Amount > dailyRemaining)
        {
            throw new LimitExceededException($"Daily limit exceeded. Remaining: {dailyRemaining}, Requested: {amount.Amount}");
        }

        var transactionsRemaining = (int)PerTransactionLimit.Amount - dailyCount;
        if (transactionsRemaining <= 0)
        {
            throw new LimitExceededException("Daily transaction limit reached");
        }
    }
}

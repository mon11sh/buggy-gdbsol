using TransactionsService.Config;
using TransactionsService.Domain.Exceptions;

namespace TransactionsService.Utils;

public static class Validators
{
    public static void ValidateAmount(decimal amount, decimal minimum, decimal maximum)
    {
        if (amount <= 0)
            throw new InvalidAmountException("Amount must be greater than zero");
            
        if (amount < minimum)
            throw new InvalidAmountException($"Amount must be at least {minimum}");
            
        if (amount > maximum)
            throw new InvalidAmountException($"Amount must not exceed {maximum}");
    }

    public static void ValidateBalance(decimal balance, decimal withdrawalAmount)
    {
        if (balance < withdrawalAmount)
            throw new InsufficientFundsException(0, "Insufficient funds");
    }

    public static void ValidateTransferLimit(decimal currentDailyTotal, decimal amount, decimal dailyLimit, decimal perTransactionLimit)
    {
        if (amount > perTransactionLimit)
            throw new LimitExceededException($"Transfer amount exceeds per-transaction limit of {perTransactionLimit}");
            
        if (currentDailyTotal + amount > dailyLimit)
            throw new LimitExceededException($"Transfer amount exceeds daily limit of {dailyLimit}");
    }
}

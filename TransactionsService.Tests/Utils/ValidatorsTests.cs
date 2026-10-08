using Microsoft.VisualStudio.TestTools.UnitTesting;
using TransactionsService.Domain.Exceptions;
using TransactionsService.Utils;

namespace TransactionsService.Tests.Utils;

/// <summary>
/// Unit coverage for the money-guard validators that were previously untested:
/// amount bounds (min/max), sufficient-funds, and daily/per-transaction transfer limits.
/// </summary>
[TestClass]
public class ValidatorsTests
{
    // ---- ValidateAmount ----

    [TestMethod]
    public void ValidateAmount_WithinBounds_DoesNotThrow()
        => Validators.ValidateAmount(500m, minimum: 100m, maximum: 100000m);

    [TestMethod]
    public void ValidateAmount_ZeroOrNegative_Throws()
        => Assert.ThrowsExactly<InvalidAmountException>(() => Validators.ValidateAmount(0m, 100m, 100000m));

    [TestMethod]
    public void ValidateAmount_BelowMinimum_Throws()
        => Assert.ThrowsExactly<InvalidAmountException>(() => Validators.ValidateAmount(50m, 100m, 100000m));

    [TestMethod]
    public void ValidateAmount_AboveMaximum_Throws()
        => Assert.ThrowsExactly<InvalidAmountException>(() => Validators.ValidateAmount(100001m, 100m, 100000m));

    [TestMethod]
    public void ValidateAmount_ExactlyAtBounds_DoesNotThrow()
    {
        Validators.ValidateAmount(100m, minimum: 100m, maximum: 100000m);      // exactly min
        Validators.ValidateAmount(100000m, minimum: 100m, maximum: 100000m);   // exactly max
    }

    // ---- ValidateBalance ----

    [TestMethod]
    public void ValidateBalance_SufficientFunds_DoesNotThrow()
        => Validators.ValidateBalance(balance: 1000m, withdrawalAmount: 400m);

    [TestMethod]
    public void ValidateBalance_ExactBalance_DoesNotThrow()
        => Validators.ValidateBalance(balance: 400m, withdrawalAmount: 400m);

    [TestMethod]
    public void ValidateBalance_InsufficientFunds_Throws()
        => Assert.ThrowsExactly<InsufficientFundsException>(() => Validators.ValidateBalance(100m, 400m));

    // ---- ValidateTransferLimit ----

    [TestMethod]
    public void ValidateTransferLimit_WithinLimits_DoesNotThrow()
        => Validators.ValidateTransferLimit(currentDailyTotal: 10000m, amount: 5000m, dailyLimit: 50000m, perTransactionLimit: 25000m);

    [TestMethod]
    public void ValidateTransferLimit_ExceedsPerTransaction_Throws()
        => Assert.ThrowsExactly<LimitExceededException>(() => Validators.ValidateTransferLimit(0m, 30000m, 50000m, 25000m));

    [TestMethod]
    public void ValidateTransferLimit_ExceedsDailyTotal_Throws()
        => Assert.ThrowsExactly<LimitExceededException>(() => Validators.ValidateTransferLimit(48000m, 5000m, 50000m, 25000m));

    [TestMethod]
    public void ValidateTransferLimit_ExactlyAtDailyLimit_DoesNotThrow()
        => Validators.ValidateTransferLimit(currentDailyTotal: 45000m, amount: 5000m, dailyLimit: 50000m, perTransactionLimit: 25000m);
}

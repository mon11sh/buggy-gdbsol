using AccountsService.Domain.Enums;
using AccountsService.Domain.Exceptions;
using AccountsService.Domain.Models;

namespace AccountsService.Tests.Domain;

/// <summary>
/// Pins the state guard order: a CLOSED account must raise <see cref="AccountClosedError"/>, not
/// the generic inactive error. (It used to be unreachable because CLOSED is also "not ACTIVE".)
/// </summary>
[TestClass]
public class AccountStateTests
{
    private static SavingsAccount WithStatus(AccountStatus status) =>
        SavingsAccount.RestoreSavings(
            new AccountId(1001), "SAVINGS", "Jane Doe", "SILVER", "hash", new Money(5000m),
            new Bank("Bank", "Branch", "GDB0000001"), status, DateTime.UtcNow, DateTime.UtcNow,
            new SavingsDetails("1990-01-01", "F", "9876543210", "enc", "idx"));

    [TestMethod]
    public void Closed_account_reports_AccountClosedError()
    {
        var closed = WithStatus(AccountStatus.CLOSED);

        Assert.ThrowsExactly<AccountClosedError>(() => closed.Credit(new Money(10m)));
        Assert.ThrowsExactly<AccountClosedError>(() => closed.Debit(new Money(10m)));
    }

    [TestMethod]
    public void Suspended_account_reports_AccountInactiveError()
    {
        var suspended = WithStatus(AccountStatus.SUSPENDED);

        Assert.ThrowsExactly<AccountInactiveError>(() => suspended.Credit(new Money(10m)));
    }

    [TestMethod]
    public void Active_account_operates()
    {
        var active = WithStatus(AccountStatus.ACTIVE);

        active.Credit(new Money(10m));
        Assert.AreEqual(5010m, active.Balance.Amount);
    }
}

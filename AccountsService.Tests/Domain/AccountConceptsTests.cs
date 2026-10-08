using System;
using AccountsService.Domain.Enums;
using AccountsService.Domain.Models;
using AccountsService.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccountsService.Tests.Domain;

/// <summary>
/// Data-driven ([DataRow]) tests for the polymorphic account behaviour:
/// abstract minimum balance, virtual maintenance fee, ref-based deduction,
/// Func-based interest policy, and the Money arithmetic overloads.
/// </summary>
[TestClass]
public class AccountConceptsTests
{
    // ---- builders -----------------------------------------------------------
    private static SavingsAccount NewSavings(string privilege = "SILVER", decimal balance = 5000m,
        AccountStatus status = AccountStatus.ACTIVE)
        => SavingsAccount.RestoreSavings(new AccountId(1001), "SAVINGS", "Test Holder", privilege, "hash",
            new Money(balance), new Bank("GDB", "Main", "GDB0000001"), status, DateTime.UtcNow, null,
            new SavingsDetails("1990-01-01", "M", "9999999999", "123412341234", "aadhaar-hash"));

    private static CurrentAccount NewCurrent(string privilege = "GOLD", decimal balance = 10000m,
        AccountStatus status = AccountStatus.ACTIVE)
        => CurrentAccount.RestoreCurrent(new AccountId(2001), "CURRENT", "Acme Corp", privilege, "hash",
            new Money(balance), new Bank("GDB", "Main", "GDB0000001"), status, DateTime.UtcNow, null,
            new CurrentDetails("Acme Corp", "REG12345678901234567", "https://acme.example"));

    private static Account Build(string type, string privilege = "SILVER", decimal balance = 5000m)
        => type == "SAVINGS" ? NewSavings(privilege, balance) : NewCurrent(privilege, balance);

    // ---- abstract method: GetMinimumBalance --------------------------------
    [TestMethod]
    [DataRow("SAVINGS", 1000.0)]
    [DataRow("CURRENT", 5000.0)]
    public void GetMinimumBalance_IsPerAccountType(string type, double expected)
        => Assert.AreEqual((decimal)expected, Build(type).GetMinimumBalance());

    // ---- virtual method + override: GetMonthlyMaintenanceFee -----------------
    [TestMethod]
    [DataRow("SAVINGS", 0.0)]
    [DataRow("CURRENT", 500.0)]
    public void GetMonthlyMaintenanceFee_UsesOverrideForCurrent(string type, double expected)
        => Assert.AreEqual((decimal)expected, Build(type).GetMonthlyMaintenanceFee());

    // ---- ref parameter: FeeCalculator.Deduct --------------------------------
    [TestMethod]
    [DataRow(1000.0, 500.0, 500.0)]
    [DataRow(100.0, 30.0, 70.0)]
    [DataRow(250.0, 0.0, 250.0)]
    public void FeeCalculator_Deduct_MutatesBalanceByRef(double start, double fee, double expected)
    {
        decimal balance = (decimal)start;
        FeeCalculator.Deduct(ref balance, (decimal)fee);
        Assert.AreEqual((decimal)expected, balance);
    }

    // ---- virtual fee applied through ref helper -----------------------------
    [TestMethod]
    [DataRow(10000.0, 9500.0)]  // fee 500 deducted
    [DataRow(300.0, 0.0)]       // fee capped at balance so it never goes negative
    public void ApplyMonthlyMaintenanceFee_Current_DeductsCappedFee(double balance, double expected)
    {
        var account = NewCurrent(balance: (decimal)balance);
        account.ApplyMonthlyMaintenanceFee();
        Assert.AreEqual((decimal)expected, account.Balance.Amount);
    }

    [TestMethod]
    public void ApplyMonthlyMaintenanceFee_InvokesAuditDelegate()
    {
        string? captured = null;
        NewCurrent(balance: 10000m).ApplyMonthlyMaintenanceFee(msg => captured = msg);
        Assert.IsNotNull(captured);
        StringAssert.Contains(captured, "Maintenance fee");
    }

    [TestMethod]
    public void ApplyMonthlyMaintenanceFee_Savings_NoFee_LeavesBalance()
    {
        var account = NewSavings(balance: 5000m);
        account.ApplyMonthlyMaintenanceFee();
        Assert.AreEqual(5000m, account.Balance.Amount);
    }

    // ---- delegate map: InterestPolicy / CalculateMonthlyInterest -------------
    [TestMethod]
    [DataRow("SILVER", 12000.0, 30.0)]
    [DataRow("GOLD", 12000.0, 40.0)]
    [DataRow("PREMIUM", 12000.0, 50.0)]
    [DataRow("UNRANKED", 12000.0, 0.0)]  // unknown privilege → NoInterest fallback
    public void CalculateMonthlyInterest_UsesPrivilegeDelegate(string privilege, double balance, double expected)
        => Assert.AreEqual((decimal)expected, Build("SAVINGS", privilege, (decimal)balance).CalculateMonthlyInterest());

    // ---- method overloading: Money.Add / Money.Subtract ----------------------
    [TestMethod]
    [DataRow(100.0, 50.0, 150.0)]
    [DataRow(0.0, 25.0, 25.0)]
    public void Money_Add_DecimalOverload_MatchesMoneyOverload(double start, double amount, double expected)
    {
        var money = new Money((decimal)start);
        Assert.AreEqual((decimal)expected, money.Add((decimal)amount).Amount);
        Assert.AreEqual(money.Add(new Money((decimal)amount)).Amount, money.Add((decimal)amount).Amount);
    }

    [TestMethod]
    [DataRow(100.0, 40.0, 60.0)]
    [DataRow(500.0, 500.0, 0.0)]
    public void Money_Subtract_DecimalOverload_Reduces(double start, double amount, double expected)
        => Assert.AreEqual((decimal)expected, new Money((decimal)start).Subtract((decimal)amount).Amount);

    // ---- exception-asserting, data-driven -----------------------------------
    [TestMethod]
    [DataRow(-1.0)]
    [DataRow(-0.01)]
    public void Money_NegativeDecimalOverloadResult_Throws(double amount)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Money(0m).Add((decimal)amount));

    [TestMethod]
    [DataRow(100.0, 140.0)]
    [DataRow(0.0, 1.0)]
    public void Money_SubtractDecimal_BelowZero_Throws(double start, double amount)
        => Assert.ThrowsExactly<InvalidOperationException>(() => new Money((decimal)start).Subtract((decimal)amount));
}

using System;
using AccountsService.Domain.Enums;

namespace AccountsService.Domain.Models;

/// <summary>
/// Domain model representing a business current account.
/// </summary>
public class CurrentAccount : Account
{
    public CurrentDetails Details { get; private set; }

    protected CurrentAccount(
        AccountId? accountNumber,
        string accountType,
        string name,
        string privilege,
        string pinHash,
        Money balance,
        Bank bankDetails,
        AccountStatus status,
        DateTime activatedDate,
        DateTime? statusUpdatedDate,
        CurrentDetails details) 
        : base(accountNumber, accountType, name, privilege, pinHash, balance, bankDetails, status, activatedDate, statusUpdatedDate)
    {
        Details = details;
    }

    /// <summary>
    /// Factory method to open a new Current Account.
    /// </summary>
    public static CurrentAccount Open(
        string name,
        string privilege,
        string pinHash,
        CurrentDetails details,
        string bankName = "Global Digital Bank",
        string bankBranch = "Main Branch",
        string ifscCode = "GDB0000001")
    {
        return new CurrentAccount(
            accountNumber: null,
            accountType: "CURRENT",
            name: name,
            privilege: privilege,
            pinHash: pinHash,
            balance: new Money(AccountRulesConfig.CURRENT_OPENING_BALANCE),
            bankDetails: new Bank(bankName, bankBranch, ifscCode),
            status: AccountStatus.ACTIVE,
            activatedDate: DateTime.UtcNow,
            statusUpdatedDate: null,
            details: details
        );
    }

    public static CurrentAccount RestoreCurrent(
        AccountId accountNumber,
        string accountType,
        string name,
        string privilege,
        string pinHash,
        Money balance,
        Bank bankDetails,
        AccountStatus status,
        DateTime activatedDate,
        DateTime? statusUpdatedDate,
        CurrentDetails details)
    {
        return new CurrentAccount(accountNumber, accountType, name, privilege, pinHash, balance, bankDetails, status, activatedDate, statusUpdatedDate, details);
    }

    public void UpdateDetails(string accountHolderName, string? website)
    {
        Details = new CurrentDetails(accountHolderName, Details.RegistrationNumber, website);
    }

    // Abstract method implementation: business current accounts carry a higher minimum balance.
    public override decimal GetMinimumBalance() => AccountRulesConfig.CURRENT_MIN_BALANCE;

    // Virtual override: current accounts are charged a monthly maintenance fee.
    public override decimal GetMonthlyMaintenanceFee() => AccountRulesConfig.CURRENT_MONTHLY_FEE;
}

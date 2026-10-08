using System;
using AccountsService.Domain.Enums;
using AccountsService.Domain.Exceptions;

namespace AccountsService.Domain.Models;

/// <summary>
/// Domain model representing a personal savings account.
/// </summary>
public class SavingsAccount : Account
{
    public SavingsDetails Details { get; private set; }

    protected SavingsAccount(
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
        SavingsDetails details) 
        : base(accountNumber, accountType, name, privilege, pinHash, balance, bankDetails, status, activatedDate, statusUpdatedDate)
    {
        Details = details;
    }

    /// <summary>
    /// Factory method to open a new Savings Account, validating all domain invariants.
    /// </summary>
    public static SavingsAccount Open(
        string name,
        string privilege,
        string pinHash,
        SavingsDetails details,
        Money initialBalance,
        DateTime today,
        string bankName = "Global Digital Bank",
        string bankBranch = "Main Branch",
        string ifscCode = "GDB0000001")
    {
        var dob = DateTime.Parse(details.DateOfBirth);
        int age = today.Year - dob.Year;
        if (dob.Date > today.AddYears(-age)) age--;

        if (age < AccountRulesConfig.MIN_SAVINGS_AGE)
            throw new AgeRestrictionError(AccountRulesConfig.MIN_SAVINGS_AGE);

        if (initialBalance.Amount < AccountRulesConfig.MIN_SAVINGS_INITIAL_BALANCE)
            throw new ValidationError("initial_balance", $"Initial balance must be >= {AccountRulesConfig.MIN_SAVINGS_INITIAL_BALANCE}");

        return new SavingsAccount(
            accountNumber: null,
            accountType: "SAVINGS",
            name: name,
            privilege: privilege,
            pinHash: pinHash,
            balance: initialBalance,
            bankDetails: new Bank(bankName, bankBranch, ifscCode),
            status: AccountStatus.ACTIVE,
            activatedDate: DateTime.UtcNow,
            statusUpdatedDate: null,
            details: details
        );
    }

    public static SavingsAccount RestoreSavings(
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
        SavingsDetails details)
    {
        return new SavingsAccount(accountNumber, accountType, name, privilege, pinHash, balance, bankDetails, status, activatedDate, statusUpdatedDate, details);
    }

    public void UpdateDetails(string phoneNumber)
    {
        Details = new SavingsDetails(Details.DateOfBirth, Details.Gender, phoneNumber, Details.Aadhaar, Details.AadhaarHash);
    }

    // Abstract method implementation: savings accounts keep a modest minimum balance.
    public override decimal GetMinimumBalance() => AccountRulesConfig.SAVINGS_MIN_BALANCE;

    // Savings accounts are free, so the base (zero) maintenance fee is inherited — no override.
}

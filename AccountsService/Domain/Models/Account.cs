using System;
using AccountsService.Domain.Enums;
using AccountsService.Domain.Exceptions;

namespace AccountsService.Domain.Models;

public abstract class Account
{
    public AccountId? AccountNumber { get; private set; }
    public string AccountType { get; private set; }
    public string Name { get; private set; }
    public string Privilege { get; private set; }
    public string PinHash { get; private set; }
    public Money Balance { get; private set; }
    public Bank BankDetails { get; private set; }
    public AccountStatus Status { get; private set; }
    public DateTime ActivatedDate { get; private set; }
    public DateTime? StatusUpdatedDate { get; private set; }


    protected Account(
        AccountId? accountNumber,
        string accountType,
        string name,
        string privilege,
        string pinHash,
        Money balance,
        Bank bankDetails,
        AccountStatus status,
        DateTime activatedDate,
        DateTime? statusUpdatedDate)
    {
        AccountNumber = accountNumber;
        AccountType = accountType;
        Name = name;
        Privilege = privilege;
        PinHash = pinHash;
        Balance = balance;
        BankDetails = bankDetails;
        Status = status;
        ActivatedDate = activatedDate;
        StatusUpdatedDate = statusUpdatedDate;
    }


    public void AssignNumber(AccountId accountNumber)
    {
        AccountNumber = accountNumber;
    }

    public void Credit(Money amount)
    {
        RequireOperable();
        Balance = Balance.Add(amount);
    }

    public void Debit(Money amount)
    {
        RequireOperable();
        if (amount.Amount > Balance.Amount)
            throw new InsufficientFundsError(Balance.Amount, amount.Amount);
        Balance = Balance.Subtract(amount);
    }

    public void Activate()
    {
        if (Status == AccountStatus.ACTIVE)
            throw new AccountAlreadyActiveError(AccountNumber?.Value);
        Status = AccountStatus.ACTIVE;
        ActivatedDate = DateTime.UtcNow;
        StatusUpdatedDate = ActivatedDate;
    }

    public void Suspend()
    {
        if (Status == AccountStatus.SUSPENDED)
            throw new AccountAlreadyInactiveError(AccountNumber?.Value);
        Status = AccountStatus.SUSPENDED;
        StatusUpdatedDate = DateTime.UtcNow;
    }
    
    public void Inactivate()
    {
        Suspend(); // Legacy compatibility name
    }

    public void Close(DateTime? when = null)
    {
        Status = AccountStatus.CLOSED;
        StatusUpdatedDate = when ?? DateTime.UtcNow;
    }

    /// <summary>
    /// The regulatory minimum balance this account type must always keep.
    /// Abstract: there is no sensible default for a generic account, so every concrete
    /// subtype (Savings, Current) is forced to state its own minimum.
    /// </summary>
    public abstract decimal GetMinimumBalance();

    /// <summary>
    /// The recurring monthly maintenance fee for this account.
    /// Virtual with a zero default: most accounts are free, and a subtype overrides this
    /// only when it actually charges a fee (see <see cref="CurrentAccount"/>).
    /// </summary>
    public virtual decimal GetMonthlyMaintenanceFee() => 0m;

    /// <summary>
    /// Applies this account's (polymorphic) monthly maintenance fee to the balance.
    /// The fee amount comes from the virtual <see cref="GetMonthlyMaintenanceFee"/> (so the
    /// right per-type amount is charged), is deducted via a <c>ref</c> helper, and an optional
    /// <see cref="Action{T}"/> audit callback is invoked with a human-readable line.
    /// </summary>
    public void ApplyMonthlyMaintenanceFee(Action<string>? audit = null)
    {
        RequireOperable();

        decimal fee = GetMonthlyMaintenanceFee();
        if (fee <= 0m)
            return;

        // Never let a fee drive the balance negative (Money forbids negative amounts).
        if (fee > Balance.Amount)
            fee = Balance.Amount;

        decimal newAmount = Balance.Amount;
        Utils.FeeCalculator.Deduct(ref newAmount, fee); // ref: mutates newAmount in place
        Balance = new Money(newAmount, Balance.Currency);

        audit?.Invoke($"Maintenance fee {fee} applied to account {AccountNumber?.Value}; balance now {newAmount}.");
    }

    /// <summary>
    /// Calculates this month's interest for the account using the privilege-based
    /// <see cref="InterestPolicy"/> delegate map (Func&lt;decimal, decimal&gt;), rounded to paise.
    /// </summary>
    public decimal CalculateMonthlyInterest()
    {
        Func<decimal, decimal> rate = InterestPolicy.ResolveFor(Privilege);
        return decimal.Round(rate(Balance.Amount), 2);
    }

    private void RequireOperable()
    {
        // CLOSED first: it is also "not ACTIVE", so the generic check would shadow it (and defeat the
        // `when (ex is not AccountClosedError)` filters in the internal service).
        if (Status == AccountStatus.CLOSED)
            throw new AccountClosedError(AccountNumber?.Value);
        if (Status != AccountStatus.ACTIVE)
            throw new AccountInactiveError(AccountNumber?.Value);
    }
}

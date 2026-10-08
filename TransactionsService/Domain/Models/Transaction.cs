using System;
using TransactionsService.Domain.Exceptions;

namespace TransactionsService.Domain.Models;

public class Transaction
{
    public int TransactionId { get; protected set; }
    public AccountId Account { get; protected set; }
    public Money Amount { get; protected set; }
    public TransactionType Type { get; protected set; }
    public TransactionStatus Status { get; protected set; }
    public string Description { get; protected set; }
    public Guid ReferenceId { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime UpdatedAt { get; protected set; }

#pragma warning disable CS8618
    protected Transaction() { }
#pragma warning restore CS8618

    public Transaction(
        int transactionId,
        AccountId account,
        Money amount,
        TransactionType type,
        TransactionStatus status,
        string description,
        Guid referenceId,
        DateTime createdAt,
        DateTime updatedAt)
    {
        TransactionId = transactionId;
        Account = account;
        Amount = amount;
        Type = type;
        Status = status;
        Description = description;
        ReferenceId = referenceId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public virtual void Complete()
    {
        if (Status == TransactionStatus.SUCCESS)
            return;
            
        Status = TransactionStatus.SUCCESS;
        UpdatedAt = DateTime.UtcNow;
    }

    public virtual void Fail(string reason)
    {
        if (Status == TransactionStatus.FAILED)
            return;

        Status = TransactionStatus.FAILED;
        Description = string.IsNullOrEmpty(Description) ? reason : $"{Description} - {reason}";
        UpdatedAt = DateTime.UtcNow;
    }
}

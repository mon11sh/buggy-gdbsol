using System;
using TransactionsService.Domain.Exceptions;

namespace TransactionsService.Domain.Models;

public class FundTransfer : Transaction
{
    // The "source_account" from the model maps to the base "Account".
    public AccountId DestinationAccount { get; private set; }
    public TransferMode Mode { get; private set; }
    // Transfer-specific status (PENDING/PROCESSING/COMPLETED/FAILED). Distinct name — no longer
    // hides the base Transaction.Status (the coarse SUCCESS/FAILED), which fixes the LSP hazard
    // where the same object reported different state depending on its static type.
    public TransferStatus TransferState { get; private set; }
    public string FailureReason { get; private set; }

#pragma warning disable CS8618
    private FundTransfer() : base() { }
#pragma warning restore CS8618

    public FundTransfer(
        int transactionId,
        AccountId sourceAccount,
        AccountId destinationAccount,
        Money amount,
        TransferMode mode,
        TransferStatus status,
        string failureReason,
        string description,
        Guid referenceId,
        DateTime createdAt,
        DateTime updatedAt)
        : base(
            transactionId, 
            sourceAccount, 
            amount, 
            TransactionType.TRANSFER, 
            MapTransferStatusToTransactionStatus(status), 
            description, 
            referenceId, 
            createdAt, 
            updatedAt)
    {
        DestinationAccount = destinationAccount;
        Mode = mode;
        TransferState = status;
        FailureReason = failureReason;
    }

    private static TransactionStatus MapTransferStatusToTransactionStatus(TransferStatus status)
    {
        return status == TransferStatus.COMPLETED ? TransactionStatus.SUCCESS : TransactionStatus.FAILED;
    }

    public static FundTransfer Create(
        AccountId sourceAccount, 
        AccountId destinationAccount, 
        Money amount, 
        TransferMode mode)
    {
        return new FundTransfer(
            0,
            sourceAccount,
            destinationAccount,
            amount,
            mode,
            TransferStatus.PENDING,
            string.Empty,
            $"Transfer to {destinationAccount.Number}",
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow
        );
    }

    public void Execute()
    {
        if (TransferState != TransferStatus.PENDING)
            throw new InvalidOperationException("Only PENDING transfers can be executed.");
        
        MarkProcessing();
    }

    public void MarkProcessing()
    {
        TransferState = TransferStatus.PROCESSING;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        TransferState = TransferStatus.COMPLETED;
        base.Complete(); // Updates base TransactionStatus.SUCCESS
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        TransferState = TransferStatus.FAILED;
        FailureReason = reason;
        base.Fail(reason); // Updates base TransactionStatus.FAILED
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// The credit leg failed and the compensating refund ALSO failed: the source has been
    /// debited and not refunded. Distinct from <see cref="MarkFailed"/> so the reconciler and
    /// operators can find exactly the transfers where money is currently in limbo.
    /// </summary>
    public void MarkCompensationFailed(string reason)
    {
        TransferState = TransferStatus.COMPENSATION_FAILED;
        FailureReason = reason;
        base.Fail(reason);
        UpdatedAt = DateTime.UtcNow;
    }
}

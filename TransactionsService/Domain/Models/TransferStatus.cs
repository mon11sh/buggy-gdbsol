namespace TransactionsService.Domain.Models;

public enum TransferStatus
{
    PENDING,
    PROCESSING,
    COMPLETED,
    FAILED,
    /// <summary>
    /// The destination credit failed AND the automatic refund of the source also failed.
    /// Money has left the source account and has not been returned. The row is a hard signal
    /// for the reconciliation worker / operations — it must never be silently retried.
    /// </summary>
    COMPENSATION_FAILED
}

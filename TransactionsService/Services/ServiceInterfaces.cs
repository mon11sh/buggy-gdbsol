using TransactionsService.Domain.Models;
using TransactionsService.DTOs;

namespace TransactionsService.Services;

// Application-service abstractions so controllers depend on interfaces, not concretes (DIP).

public interface IDepositService
{
    Task<TransactionResultResponse> ProcessDepositAsync(int accountNumber, decimal amount, string description, string? idempotencyKey, CancellationToken ct = default);
}

public interface IWithdrawService
{
    Task<TransactionResultResponse> ProcessWithdrawAsync(int accountNumber, decimal amount, string pin, string description, string? idempotencyKey, CancellationToken ct = default);
}

public interface ITransferService
{
    Task<TransferResultResponse> ProcessTransferAsync(int fromAccount, int toAccount, decimal amount, string pin, TransferMode transferMode, string description, string? idempotencyKey, CancellationToken ct = default);
}

public interface ITransactionLogService
{
    Task<PagedTransactionResponse> GetTransactionLogsAsync(int accountId, int skip, int limit, DateTime? startDate, DateTime? endDate, string? type, CancellationToken ct = default);
    Task<PagedTransactionResponse> GetAllTransactionsAsync(int skip, int limit, string? type, DateTime? startDate, DateTime? endDate, CancellationToken ct = default);
    Task<TransactionDetailsResponse> GetTransactionDetailsAsync(int transactionId, CancellationToken ct = default);
    /// <summary>A fund transfer with its ledger legs; null when the id is unknown.</summary>
    Task<TransferDetailsResponse?> GetTransferDetailsAsync(int transferId, CancellationToken ct = default);
}
